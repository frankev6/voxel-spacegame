using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Persistence.Data;

namespace Game.Persistence.Services
{
    /// <summary>Implements SPEC-05 §4.5. Profiles, slots, save and load (managed service, architecture §4.2 service #6).</summary>
    public sealed class SaveService : IDisposable
    {
        readonly SavePaths paths;
        readonly Game.Data.Services.ContentService content;
        readonly Game.Core.IPauseService pause;
        readonly SaveServiceConfig config;
        readonly SaveChunkRegistry registry;
        readonly NewtonsoftSaveJsonCodec json = new NewtonsoftSaveJsonCodec();
        ProfileId activeProfile;
        SlotRef? currentSlot;

        /// <summary>PERS-002 stores the dependencies; PERS-005 adds participant registration and the operation queue.</summary>
        public SaveService(SavePaths paths, Game.Data.Services.ContentService content, Game.Core.IPauseService pause,
                           SaveServiceConfig config, SaveChunkRegistry registry = null)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            this.content = content;
            this.pause = pause;
            this.config = config ?? new SaveServiceConfig();
            this.registry = registry ?? SaveChunkRegistry.Default;
        }
        public void AttachContext(ISaveContext context) { throw new System.NotImplementedException("PERS-005"); }
        public void Register(Game.Core.Save.ISaveParticipant participant) { throw new System.NotImplementedException("PERS-005"); }   // InvalidOperationException: unknown key, duplicate key, META/IDPL

        // Profiles
        #region Profiles and slots (PERS-002, SPEC-05 §5.2)

        /// <summary>Every readable Root/p???/profile.json, sorted by CreatedUtc then ID; unreadable files are skipped with one warning each.</summary>
        public IReadOnlyList<ProfileInfo> ListProfiles()
        {
            var result = new List<ProfileInfo>();
            if (!Directory.Exists(paths.Root)) return result;
            foreach (var dir in Directory.GetDirectories(paths.Root))
            {
                string name = Path.GetFileName(dir);
                if (!ProfileFolder.IsMatch(name)) continue;
                var id = new ProfileId(name);
                string file = paths.ProfileFile(id);
                if (!File.Exists(file)) continue;
                try
                {
                    var d = json.Deserialize<ProfileDescriptor>(File.ReadAllBytes(file));
                    result.Add(new ProfileInfo { Id = id, DisplayName = d.DisplayName, CreatedUtc = d.CreatedUtc, LastSlotId = d.LastSlotId });
                }
                catch (Exception e) when (e is IOException || e is SaveFormatException || e is UnauthorizedAccessException)
                { Game.Core.GameLog.Warn("Save", $"Skipping unreadable profile {file}: {e.Message}"); }
            }
            result.Sort((a, b) => a.CreatedUtc != b.CreatedUtc ? a.CreatedUtc.CompareTo(b.CreatedUtc) : string.CompareOrdinal(a.Id.Value, b.Id.Value));
            return result;
        }

        /// <summary>Trims the name (empty → "Commander", at most 32 characters), takes the lowest free p001..p999, writes profile.json and makes it active.</summary>
        public ProfileInfo CreateProfile(string displayName)
        {
            string name = (displayName ?? "").Trim();
            if (name.Length == 0) name = "Commander";
            if (name.Length > 32) name = name.Substring(0, 32);
            ProfileId id = default;
            for (int n = 1; n <= 999 && id.Value == null; n++)
            {
                var candidate = new ProfileId("p" + n.ToString("000", CultureInfo.InvariantCulture));
                if (!Directory.Exists(paths.ProfileDir(candidate))) id = candidate;
            }
            if (id.Value == null) throw new InvalidOperationException("All 999 profile ids are in use.");
            var descriptor = new ProfileDescriptor { Id = id.Value, DisplayName = name, CreatedUtc = DateTime.UtcNow, LastSlotId = null };
            Directory.CreateDirectory(paths.ProfileDir(id));
            WriteReplacing(paths.ProfileFile(id), json.Serialize(descriptor));
            activeProfile = id;
            return new ProfileInfo { Id = id, DisplayName = name, CreatedUtc = descriptor.CreatedUtc, LastSlotId = null };
        }

        /// <summary>Deletes the profile folder; if it was active, the first remaining profile (or none) becomes active. False if it did not exist.</summary>
        public bool DeleteProfile(ProfileId id)
        {
            if (id.Value == null || !Directory.Exists(paths.ProfileDir(id))) return false;
            Directory.Delete(paths.ProfileDir(id), recursive: true);
            if (activeProfile.Equals(id))
            {
                var remaining = ListProfiles();
                activeProfile = remaining.Count > 0 ? remaining[0].Id : default;
            }
            if (currentSlot.HasValue && currentSlot.Value.Profile.Equals(id)) currentSlot = null;
            return true;
        }

        public ProfileId ActiveProfile => activeProfile;
        public void SetActiveProfile(ProfileId id) { activeProfile = id; }

        // Slots
        /// <summary>Every manual_NNN, quick and auto_N slot of the profile with its status (§5.2), newest first; never throws.</summary>
        public IReadOnlyList<SlotInfo> ListSlots(ProfileId profile)
        {
            var result = new List<SlotInfo>();
            try
            {
                string dir = profile.Value == null ? null : paths.ProfileDir(profile);
                if (dir == null || !Directory.Exists(dir)) return result;
                foreach (var slotDir in Directory.GetDirectories(dir))
                {
                    string name = Path.GetFileName(slotDir);
                    if (!SlotFolder.IsMatch(name)) continue;
                    result.Add(ReadSlot(new SlotRef(profile, new SlotId(name))));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { Game.Core.GameLog.Warn("Save", $"Listing slots of {profile} failed: {e.Message}"); }
            result.Sort((a, b) =>
            {
                if (a.Meta != null && b.Meta != null && a.Meta.SavedUtc != b.Meta.SavedUtc) return b.Meta.SavedUtc.CompareTo(a.Meta.SavedUtc);
                if ((a.Meta == null) != (b.Meta == null)) return a.Meta == null ? 1 : -1;
                return string.CompareOrdinal(a.Ref.Slot.Value, b.Ref.Slot.Value);
            });
            return result;
        }

        /// <summary>The next manual slot after the highest existing number (no folder is created); false when it would exceed MaxManualSlots.</summary>
        public bool TryCreateManualSlot(ProfileId profile, out SlotRef slot)
        {
            int max = 0;
            string dir = paths.ProfileDir(profile);
            if (Directory.Exists(dir))
                foreach (var d in Directory.GetDirectories(dir))
                {
                    var m = ManualFolder.Match(Path.GetFileName(d));
                    if (m.Success) max = Math.Max(max, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
                }
            int next = max + 1;
            if (next > config.MaxManualSlots || next > 999) { slot = default; return false; }
            slot = new SlotRef(profile, new SlotId("manual_" + next.ToString("000", CultureInfo.InvariantCulture)));
            return true;
        }

        public SlotRef QuickSlot(ProfileId profile) => new SlotRef(profile, new SlotId("quick"));

        /// <summary>Among auto_0..auto_{n-1}: the first one without a readable save time, else the oldest SavedUtc (ties → lowest index).</summary>
        public SlotRef NextAutosaveSlot(ProfileId profile)
        {
            SlotRef best = default;
            DateTime oldest = DateTime.MaxValue;
            for (int i = 0; i < Math.Max(1, config.AutosaveSlotCount); i++)
            {
                var slot = new SlotRef(profile, new SlotId("auto_" + i.ToString(CultureInfo.InvariantCulture)));
                var meta = ReadSlot(slot).Meta;
                if (meta == null) return slot;
                if (meta.SavedUtc < oldest) { oldest = meta.SavedUtc; best = slot; }
            }
            return best;
        }

        /// <summary>Deletes the slot folder; clears CurrentSlot if it is this slot. False if the folder did not exist.</summary>
        public bool DeleteSlot(SlotRef slot)
        {
            string dir = paths.SlotDir(slot);
            if (!Directory.Exists(dir)) return false;
            Directory.Delete(dir, recursive: true);
            if (currentSlot.HasValue && currentSlot.Value.Equals(slot)) currentSlot = null;
            return true;
        }

        public bool HasCurrentSlot => currentSlot.HasValue;
        public SlotRef CurrentSlot => currentSlot ?? default;       // last slot saved to or loaded from in this session

        /// <summary>Set by a successful save or load (PERS-005/006).</summary>
        internal void MarkCurrentSlot(SlotRef slot) { currentSlot = slot; }

        static readonly Regex ProfileFolder = new Regex("^p[0-9]{3}$", RegexOptions.CultureInvariant);
        static readonly Regex SlotFolder = new Regex("^(manual_[0-9]{3}|quick|auto_[0-9])$", RegexOptions.CultureInvariant);
        static readonly Regex ManualFolder = new Regex("^manual_([0-9]{3})$", RegexOptions.CultureInvariant);

        /// <summary>Status rules of §5.2. A valid meta.json gives Ok when world.sav exists. Otherwise (and when meta.json is missing or invalid):
        /// world.sav → MetaMissing (Meta from its META chunk when readable), only world.bak → BackupOnly, nothing → Corrupt.
        /// MetaVersion above 1 → NewerFormat.</summary>
        SlotInfo ReadSlot(SlotRef slot)
        {
            var info = new SlotInfo { Ref = slot, Kind = KindOf(slot.Slot.Value) };
            string world = paths.WorldFile(slot), backup = paths.BackupFile(slot), metaFile = paths.MetaFile(slot);
            SaveMeta meta = null;
            try
            {
                if (File.Exists(metaFile))
                {
                    var bytes = File.ReadAllBytes(metaFile);
                    var version = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes))["MetaVersion"];
                    if (version != null && version.Type == Newtonsoft.Json.Linq.JTokenType.Integer && (int)version > 1)
                    { info.Status = SlotStatus.NewerFormat; info.WorldBytes = SizeOf(world); return info; }
                    meta = json.Deserialize<SaveMeta>(bytes);
                }
            }
            catch (Exception e) when (e is IOException || e is SaveFormatException || e is UnauthorizedAccessException || e is Newtonsoft.Json.JsonException)
            { meta = null; }
            if (File.Exists(world))
            {
                info.WorldBytes = SizeOf(world);
                if (meta != null) { info.Status = SlotStatus.Ok; info.Meta = meta; return info; }
                info.Status = SlotStatus.MetaMissing;
                info.Meta = TryReadMetaChunk(world);
                return info;
            }
            if (File.Exists(backup)) { info.Status = SlotStatus.BackupOnly; info.Meta = meta; info.WorldBytes = SizeOf(backup); return info; }
            info.Status = SlotStatus.Corrupt;
            return info;
        }

        /// <summary>Reads the META chunk from the first 64 KB of world.sav (header + table + META, CRCs checked). Null when unreadable.</summary>
        SaveMeta TryReadMetaChunk(string world)
        {
            try
            {
                byte[] head;
                using (var s = new FileStream(world, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    head = new byte[(int)Math.Min(s.Length, 64 * 1024)];
                    int read = 0;
                    while (read < head.Length) { int n = s.Read(head, read, head.Length - read); if (n <= 0) break; read += n; }
                }
                if (SaveContainerReader.TryReadChunkFromPrefix(head, "META", out var payload)) return json.Deserialize<SaveMeta>(payload);
            }
            catch (Exception) { }   // ListSlots never throws: any failure here means "no META available"
            return null;
        }

        static SlotKind KindOf(string slotId)
            => slotId == "quick" ? SlotKind.Quick : slotId.StartsWith("auto_", StringComparison.Ordinal) ? SlotKind.Auto : SlotKind.Manual;

        static long SizeOf(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

        /// <summary>Writes path.tmp, then replaces (or moves onto) path.</summary>
        static void WriteReplacing(string path, byte[] bytes)
        {
            string tmp = path + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(path)) File.Replace(tmp, path, null, ignoreMetadataErrors: true);
            else File.Move(tmp, path);
        }

        #endregion

        // State and operations
        public bool IsBusy => throw new System.NotImplementedException("PERS-005");
        public bool CanSave(out SaveBlockReason reason) { throw new System.NotImplementedException("PERS-005"); }
        public void ResetForNewGame() { throw new System.NotImplementedException("PERS-005"); }                                  // clears preserved chunks, seeded palette, thumbnail cache, CurrentSlot
        public UnityEngine.Awaitable<SaveResult> SaveAsync(SlotRef slot, SaveReason reason, string displayName = null) { throw new System.NotImplementedException("PERS-005"); }
        public UnityEngine.Awaitable<LoadResult> ReadAsync(SlotRef slot) { throw new System.NotImplementedException("PERS-006"); }
        public Game.Data.Validation.ValidationReport Restore(LoadedSave save) { throw new System.NotImplementedException("PERS-006"); }   // main thread
        public Game.Data.Validation.ValidationReport LastLoadReport => throw new System.NotImplementedException("PERS-006");
#pragma warning disable CS0067 // raised by PERS-005/006
        public event Action<SaveOperationResult> OperationCompleted;
#pragma warning restore CS0067

        // Thumbnails
        public bool TryLoadThumbnail(SlotRef slot, string key, out UnityEngine.Texture2D texture) { throw new System.NotImplementedException("PERS-005"); }  // caller destroys texture
        public bool TryGetSessionThumbnail(string key, out byte[] png) { throw new System.NotImplementedException("PERS-005"); }

        public AutosaveScheduler Autosave => throw new System.NotImplementedException("PERS-009");
        public void Dispose() { throw new System.NotImplementedException("PERS-005"); }

        // Synchronous seams for tests (InternalsVisibleTo):
        internal CapturedSave CaptureNow(SlotRef slot, SaveReason reason, string displayName) { throw new System.NotImplementedException("PERS-005"); }
        internal SaveResult WriteCaptured(CapturedSave captured) { throw new System.NotImplementedException("PERS-005"); }          // thread-agnostic
        internal LoadResult ReadNow(SlotRef slot) { throw new System.NotImplementedException("PERS-006"); }                         // thread-agnostic
        internal bool HandleWantsToQuit() { throw new System.NotImplementedException("PERS-009"); }
    }
}
