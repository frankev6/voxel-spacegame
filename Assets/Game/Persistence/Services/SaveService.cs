using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Game.Core;
using Game.Core.Events;
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

        // PERS-005: participants, session state carried between saves, the operation queue.
        ISaveContext context;
        readonly List<ISaveParticipant> participants = new List<ISaveParticipant>();
        /// <summary>Unknown chunks of the loaded save, written back unchanged (decision 4); filled by PERS-006.</summary>
        internal readonly List<RawChunk> PreservedChunks = new List<RawChunk>();
        /// <summary>The loaded save's palette, seeded into the next save while preserved chunks refer to it; set by PERS-006.</summary>
        internal SaveIdPalette LoadedPalette;
        readonly Dictionary<string, byte[]> thumbnailCache = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        readonly List<object> queue = new List<object>();   // queue[0] is the running operation
        bool disposed, subscribedToQuit, quitApproved;
        /// <summary>Quit routine's final step; tests replace it (Application.Quit does nothing in edit mode).</summary>
        internal Action QuitAction = UnityEngine.Application.Quit;
        /// <summary>Test hook (§5.4 step 13): runs after world.sav.tmp is written and before it replaces world.sav.</summary>
        internal Action TestHookBeforeReplace;

        /// <summary>PERS-002 stores the dependencies; PERS-005 adds participant registration and the operation queue.</summary>
        public SaveService(SavePaths paths, Game.Data.Services.ContentService content, Game.Core.IPauseService pause,
                           SaveServiceConfig config, SaveChunkRegistry registry = null)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
            this.content = content;
            this.pause = pause;
            this.config = config ?? new SaveServiceConfig();
            this.registry = registry ?? SaveChunkRegistry.Default;
            Autosave = new AutosaveScheduler(this, this.config, pause);
            if (UnityEngine.Application.isPlaying) { UnityEngine.Application.wantsToQuit += HandleWantsToQuit; subscribedToQuit = true; }   // §5.9
        }
        public void AttachContext(ISaveContext context) { this.context = context; }

        /// <summary>Kept in registry order. InvalidOperationException for META/IDPL, a key missing from the registry, or a duplicate.</summary>
        public void Register(ISaveParticipant participant)
        {
            if (participant == null) throw new ArgumentNullException(nameof(participant));
            string key = participant.Key;
            if (key == "META" || key == "IDPL") throw new InvalidOperationException($"Save participant key {key} is reserved for the container");
            if (!registry.TryGetOrder(key, out _)) throw new InvalidOperationException($"Save participant key {key} is not in SaveChunkRegistry (SPEC-05 §4.8)");
            foreach (var p in participants) if (p.Key == key) throw new InvalidOperationException($"Save participant key {key} is already registered");
            if (participant.Version < 1 || participant.Version > ushort.MaxValue) throw new InvalidOperationException($"Save participant {key} has version {participant.Version} (1..65535)");
            participants.Add(participant);
            participants.Sort((a, b) => { registry.TryGetOrder(a.Key, out var x); registry.TryGetOrder(b.Key, out var y); return x.CompareTo(y); });
        }

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
                    if (d == null) throw new SaveFormatException("Profile JSON does not contain an object", -1);
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

        [Game.Core.StaticStateOk("immutable regex")] static readonly Regex ProfileFolder = new Regex("^p[0-9]{3}$", RegexOptions.CultureInvariant);
        [Game.Core.StaticStateOk("immutable regex")] static readonly Regex SlotFolder = new Regex("^(manual_[0-9]{3}|quick|auto_[0-9])$", RegexOptions.CultureInvariant);
        [Game.Core.StaticStateOk("immutable regex")] static readonly Regex ManualFolder = new Regex("^manual_([0-9]{3})$", RegexOptions.CultureInvariant);

        /// <summary>Status rules of §5.2. A valid meta.json gives Ok when world.sav exists. Otherwise (and when meta.json is missing or invalid):
        /// world.sav → MetaMissing (Meta from its META chunk when readable), only world.bak → BackupOnly, nothing → Corrupt.
        /// Readable embedded metadata also supersedes a valid but stale sidecar (Q24).
        /// MetaVersion above 1 → NewerFormat.</summary>
        SlotInfo ReadSlot(SlotRef slot)
        {
            var info = new SlotInfo { Ref = slot, Kind = KindOf(slot.Slot.Value) };
            string world = paths.WorldFile(slot), backup = paths.BackupFile(slot), metaFile = paths.MetaFile(slot);
            SaveMeta meta = null;
            int prefixBudget = 64 * 1024;
            try
            {
                if (File.Exists(metaFile))
                {
                    // Keep total listing I/O bounded; reserve at least half for authoritative container META.
                    using var stream = new FileStream(metaFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                    if (stream.Length > 32 * 1024) throw new SaveFormatException("Metadata sidecar exceeds listing budget", -1);
                    var bytes = new byte[(int)stream.Length];
                    int read = 0;
                    while (read < bytes.Length)
                    {
                        int n = stream.Read(bytes, read, bytes.Length - read);
                        if (n == 0) throw new EndOfStreamException();
                        read += n; prefixBudget -= n;
                    }
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
                // world.sav is the commit point; a crash or sidecar write failure may leave older, valid JSON.
                // Compare the complete metadata, not only time/size (two saves can share a millisecond).
                var embedded = TryReadMetaChunk(world, prefixBudget);
                if (embedded != null)
                {
                    embedded.WorldBytes = info.WorldBytes;
                    bool matches = meta != null && new ReadOnlySpan<byte>(json.Serialize(meta)).SequenceEqual(json.Serialize(embedded));
                    info.Status = matches ? SlotStatus.Ok : SlotStatus.MetaMissing;
                    info.Meta = embedded;
                    return info;
                }
                if (meta != null) { info.Status = SlotStatus.Ok; info.Meta = meta; return info; }
                info.Status = SlotStatus.MetaMissing;
                return info;
            }
            if (File.Exists(backup)) { info.Status = SlotStatus.BackupOnly; info.Meta = meta; info.WorldBytes = SizeOf(backup); return info; }
            info.Status = SlotStatus.Corrupt;
            return info;
        }

        /// <summary>Reads the META chunk from the first 64 KB of world.sav (header + table + META, CRCs checked). Null when unreadable.</summary>
        SaveMeta TryReadMetaChunk(string world, int maxBytes)
        {
            try
            {
                byte[] head;
                using (var s = new FileStream(world, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    head = new byte[(int)Math.Min(s.Length, maxBytes)];
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
        /// <summary>True while an operation runs or waits in the queue (§5.3).</summary>
        public bool IsBusy => queue.Count > 0;

        /// <summary>Reason from ISaveContext; NoSession without a context or an active session.</summary>
        public bool CanSave(out SaveBlockReason reason)
        {
            if (context == null || !context.HasActiveSession) { reason = SaveBlockReason.NoSession; return false; }
            reason = context.GetBlockReason();
            return reason == SaveBlockReason.None;
        }

        /// <summary>Clears preserved chunks, the seeded palette, the thumbnail cache and CurrentSlot.</summary>
        public void ResetForNewGame()
        {
            PreservedChunks.Clear();
            LoadedPalette = null;
            thumbnailCache.Clear();
            currentSlot = null;
        }

        /// <summary>§5.3 queue, then §5.4: capture on the main thread, serialize and write on a background thread, finish on the main thread.</summary>
        public async UnityEngine.Awaitable<SaveResult> SaveAsync(SlotRef slot, SaveReason reason, string displayName = null)
        {
            if (IsAutosave(reason) && IsBusy)                                         // §5.3 rule 1: the scheduler keeps it pending
                return new SaveResult(false, SaveErrorKind.Blocked, SaveMessages.Blocked(SaveBlockReason.Transitioning), slot, 0, 0, 0);
            var ticket = new object();
            if (!Enqueue(ticket, reason == SaveReason.Quit))
            {
                var full = new SaveResult(false, SaveErrorKind.IoError, SaveMessages.IoError(SaveMessages.TooManyQueued), slot, 0, 0, 0);
                RaiseCompleted(full, reason);
                return full;
            }
            try
            {
                while (queue.Count > 0 && queue[0] != ticket) await UnityEngine.Awaitable.NextFrameAsync();
                var captured = CaptureNow(slot, reason, displayName);
                SaveResult result;
                if (captured.Failure.HasValue) result = captured.Failure.Value;
                else
                {
                    await UnityEngine.Awaitable.BackgroundThreadAsync();
                    try { result = WriteCaptured(captured); }
                    finally { await UnityEngine.Awaitable.MainThreadAsync(); }
                }
                FinishSave(captured, result);
                return result;
            }
            finally { queue.Remove(ticket); }
        }

        /// <summary>§5.3 rules 2–3. False when the queue is full.</summary>
        bool Enqueue(object ticket, bool quit)
        {
            int waiting = Math.Max(0, queue.Count - 1);
            if (quit) { queue.Insert(Math.Min(1, queue.Count), ticket); return true; }
            if (queue.Count > 0 && waiting >= config.MaxQueuedOperations) return false;
            queue.Add(ticket);
            return true;
        }

        static bool IsAutosave(SaveReason r) => r == SaveReason.AutoTimer || r == SaveReason.AutoStarSystemEntered || r == SaveReason.AutoSceneTransition;
        static bool RaisesWhenBlocked(SaveReason r) => r == SaveReason.Manual || r == SaveReason.Quick || r == SaveReason.Quit;

        #region Load (PERS-006, SPEC-05 §5.6)

        /// <summary>Queued like saves (§5.3); reads on a background thread. On failure raises OperationCompleted and changes nothing else (R28).</summary>
        public async UnityEngine.Awaitable<LoadResult> ReadAsync(SlotRef slot)
        {
            var ticket = new object();
            if (!Enqueue(ticket, false))
            {
                var full = new LoadResult(false, SaveErrorKind.IoError, SaveMessages.IoError(SaveMessages.TooManyQueued), null);
                OperationCompleted?.Invoke(new SaveOperationResult(true, false, full.Error, full.Message, slot, default, 0, null));
                return full;
            }
            try
            {
                while (queue.Count > 0 && queue[0] != ticket) await UnityEngine.Awaitable.NextFrameAsync();
                LoadResult result;
                await UnityEngine.Awaitable.BackgroundThreadAsync();
                try { result = ReadNow(slot); }
                finally { await UnityEngine.Awaitable.MainThreadAsync(); }
                if (!result.Success) OperationCompleted?.Invoke(new SaveOperationResult(true, false, result.Error, result.Message, slot, default, 0, null));
                return result;
            }
            finally { queue.Remove(ticket); }
        }

        /// <summary>§5.6 steps 1–7, thread-agnostic. Never throws; never touches participants.</summary>
        internal LoadResult ReadNow(SlotRef slot)
        {
            var report = new Game.Data.Validation.ValidationReport("Load " + slot.Profile.Value + "/" + slot.Slot.Value);
            string worldFile = paths.WorldFile(slot), backupFile = paths.BackupFile(slot);
            // 1–3.
            var worldChunks = new List<RawChunk>();
            var worldReport = new Game.Data.Validation.ValidationReport(report.Context);
            var worldStatus = TryRead(worldFile, out var worldHeader, worldChunks, worldReport, "world.sav", out bool worldMissing);
            var chunks = worldChunks; var header = worldHeader; bool fromBackup = false;
            if (worldStatus == ContainerReadStatus.Ok) report.Merge(worldReport);
            else
            {
                // 4.
                if (worldStatus == ContainerReadStatus.NewerFormat)
                    return new LoadResult(false, SaveErrorKind.NewerFormat, SaveMessages.NewerFormat, null);
                var bakChunks = new List<RawChunk>();
                var bakStatus = TryRead(backupFile, out var bakHeader, bakChunks, new Game.Data.Validation.ValidationReport(report.Context), "world.bak", out bool bakMissing);
                if (bakStatus == ContainerReadStatus.Ok)
                {
                    chunks = bakChunks; header = bakHeader; fromBackup = true;
                    report.Warning("PERS-W010", "world.bak", "", "Loaded the backup save (one save older)");
                }
                else if (worldStatus == ContainerReadStatus.CorruptChunks) report.Merge(worldReport);
                else if (worldMissing && bakMissing) return new LoadResult(false, SaveErrorKind.NotFound, SaveMessages.NotFound, null);
                else return new LoadResult(false, SaveErrorKind.Unreadable, SaveMessages.Unreadable, null);
            }

            // 5.
            SaveMeta meta = null;
            var metaChunk = Find(chunks, "META");
            if (metaChunk != null && !metaChunk.Corrupt)
            {
                try
                {
                    meta = json.Deserialize<SaveMeta>(metaChunk.Payload);
                    if (meta == null) throw new SaveFormatException("META JSON does not contain an object", -1);
                }
                catch (Exception e) { report.Warning("PERS-W013", "META", "", "The save's description (META) is unreadable: " + e.Message); }
            }
            else report.Warning("PERS-W013", "META", "", "The save has no readable description (META)");
            SaveIdPalette palette;
            var idpl = Find(chunks, "IDPL");
            try
            {
                if (idpl == null || idpl.Corrupt) throw new SaveFormatException("IDPL chunk missing or corrupt", -1);
                palette = SaveIdPalette.Parse(idpl.Payload);
            }
            catch (Exception e)
            {
                report.Error("PERS-E012", "IDPL", "", "The content id table (IDPL) is unreadable; saved content becomes placeholders: " + e.Message);
                palette = SaveIdPalette.CreateEmpty();
            }

            // 6.
            var staged = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            try
            {
                string dir = paths.ThumbsDir(slot);
                if (Directory.Exists(dir))
                    foreach (var file in Directory.GetFiles(dir, "*.png")) staged[Path.GetFileNameWithoutExtension(file)] = File.ReadAllBytes(file);
            }
            catch (Exception) { }

            // 7.
            return new LoadResult(true, SaveErrorKind.None, null, new LoadedSave(slot, header, meta, fromBackup, chunks, report, palette, staged));
        }

        static ContainerReadStatus TryRead(string file, out ContainerHeader header, List<RawChunk> chunks, Game.Data.Validation.ValidationReport report, string label, out bool missing)
        {
            header = default;
            missing = !File.Exists(file);
            if (missing) return ContainerReadStatus.NotAContainer;
            try { return SaveContainerReader.Read(File.ReadAllBytes(file), out header, chunks, report, label); }
            catch (Exception) { return ContainerReadStatus.NotAContainer; }      // unreadable file (I/O): same as not a container
        }

        static RawChunk Find(IReadOnlyList<RawChunk> chunks, string key)
        {
            foreach (var c in chunks) if (c.Key == key) return c;
            return null;
        }

        /// <summary>§5.6 Restore, main thread. Never throws for data problems; a LoadedSave may be restored once.</summary>
        public Game.Data.Validation.ValidationReport Restore(LoadedSave save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (save.Restored) throw new InvalidOperationException("This LoadedSave was already restored");
            save.Restored = true;
            // 1.
            content?.Catalog.ClearUnresolved();
            var report = save.Report;
            PreservedChunks.Clear();
            LoadedPalette = save.Palette;
            var registered = new HashSet<string>(StringComparer.Ordinal);
            // 2.
            foreach (var p in participants)
            {
                registered.Add(p.Key);
                var chunk = Find(save.Chunks, p.Key);
                if (chunk == null)
                {
                    RestoreDefaults(p, report);
                    report.Info("PERS-I020", p.Key, "", $"No data for {p.Key}; defaults used");
                    continue;
                }
                if (chunk.Corrupt) { RestoreDefaults(p, report); continue; }
                if (chunk.Version > p.Version)
                {
                    PreservedChunks.Add(chunk);
                    RestoreDefaults(p, report);
                    report.Warning("PERS-W021", p.Key, "", $"{p.Key} was saved by a newer version (chunk version {chunk.Version} > {p.Version}); kept unchanged, defaults used");
                    continue;
                }
                try
                {
                    var reader = new SaveReader(chunk.Payload, save.Palette, json);
                    p.Restore(reader, chunk.Version);
                    if (!reader.IsAtEnd) report.Warning("PERS-W024", p.Key, "", $"{p.Key} left {reader.Remaining} unread bytes");
                }
                catch (Exception e)
                {
                    report.Error("PERS-E022", p.Key, "", $"{p.Key} could not be restored: {e.GetType().Name}: {e.Message}; defaults used");
                    RestoreDefaults(p, report);
                }
            }
            // 3.
            foreach (var c in save.Chunks)
            {
                if (c.Key == "META" || c.Key == "IDPL" || registered.Contains(c.Key)) continue;
                PreservedChunks.Add(c);
                report.Info("PERS-I025", c.Key, "", $"Unknown chunk {c.Key} kept unchanged");
            }
            // 4.
            if (content != null)
                foreach (var u in content.Catalog.Unresolved)
                    report.Warning("PERS-W001", u.Context, "", $"Unknown content '{u.Id}' ({u.ExpectedType}) in {u.Context}; replaced by a placeholder");
            // 5.
            if (content != null && save.Header.DatabaseHash32 != (uint)content.DatabaseHash)
                report.Info("PERS-I002", "world.sav", "", "Saved with different content");
            // 6.
            thumbnailCache.Clear();
            foreach (var kv in save.StagedThumbnails) thumbnailCache[kv.Key] = kv.Value;
            currentSlot = save.Slot;
            SetProfileLastSlot(save.Slot);
            LastLoadReport = report;
            report.LogTo("Save");
            int warnings = report.WarningCount + report.ErrorCount;
            EventBus<GameLoaded>.Publish(new GameLoaded(save.Slot.Profile.Value, save.Slot.Slot.Value, warnings));
            OperationCompleted?.Invoke(new SaveOperationResult(true, true, SaveErrorKind.None, SaveMessages.Loaded(warnings), save.Slot, default, warnings, report));
            Autosave.NotifySaved();                                       // restarts the timer
            // 7.
            return report;
        }

        /// <summary>Restore(SaveReader.Empty, 0); an exception there is PERS-E023.</summary>
        static void RestoreDefaults(ISaveParticipant p, Game.Data.Validation.ValidationReport report)
        {
            try { p.Restore(SaveReader.Empty, 0); }
            catch (Exception e) { report.Error("PERS-E023", p.Key, "", $"{p.Key} could not reset to defaults: {e.GetType().Name}: {e.Message}"); }
        }

        public Game.Data.Validation.ValidationReport LastLoadReport { get; private set; }

        #endregion
        public event Action<SaveOperationResult> OperationCompleted;

        // Thumbnails
        /// <summary>Main thread. Reads thumbs/&lt;key&gt;.png of a slot; the caller destroys the texture.</summary>
        public bool TryLoadThumbnail(SlotRef slot, string key, out UnityEngine.Texture2D texture)
        {
            texture = null;
            string file = paths.ThumbnailFile(slot, key);
            if (!File.Exists(file)) return false;
            try
            {
                var tex = new UnityEngine.Texture2D(2, 2);
                if (!UnityEngine.ImageConversion.LoadImage(tex, File.ReadAllBytes(file))) { UnityEngine.Object.DestroyImmediate(tex); return false; }
                texture = tex;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return false; }
        }

        /// <summary>The PNG of a thumbnail written (or carried) by this session's saves.</summary>
        public bool TryGetSessionThumbnail(string key, out byte[] png) => thumbnailCache.TryGetValue(key, out png);

        public AutosaveScheduler Autosave { get; }

        internal bool HasActiveSessionContext => context != null && context.HasActiveSession;

        /// <summary>Safe to call twice. Removes the Application.wantsToQuit handler (§5.9).</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (subscribedToQuit) { UnityEngine.Application.wantsToQuit -= HandleWantsToQuit; subscribedToQuit = false; }
            OperationCompleted = null;
            participants.Clear();
            context = null;
        }

        /// <summary>§5.4 steps 1–7, main thread.</summary>
        internal CapturedSave CaptureNow(SlotRef slot, SaveReason reason, string displayName)
        {
            var captured = new CapturedSave { Slot = slot, Reason = reason, Total = System.Diagnostics.Stopwatch.StartNew() };
            string stage = "session context";
            string diagnostic = "capture of " + stage;
            try
            {
                // 1.
                if (!CanSave(out var blocked))
                {
                    captured.Failure = new SaveResult(false, SaveErrorKind.Blocked, SaveMessages.Blocked(blocked), slot, 0, 0, 0);
                    return captured;
                }
                // 2.
                captured.Palette = PreservedChunks.Count > 0 && LoadedPalette != null ? SaveIdPalette.SeededFrom(LoadedPalette) : SaveIdPalette.CreateEmpty();
                // 3.
                foreach (var p in participants)
                {
                    if (IsPreserved(p.Key)) continue;                         // a newer-version chunk is written back instead (open question 19)
                    stage = p.Key;
                    diagnostic = "capture of chunk " + stage;
                    var w = new SaveWriter(captured.Palette, json);
                    captured.Participants.Add((p.Key, (ushort)p.Version, w));
                    p.Capture(w);
                }
                stage = "session metadata";
                diagnostic = "capture of " + stage;
                // 4. SavedUtc is cut to whole milliseconds: the JSON keeps milliseconds, and the header ticks must match META exactly.
                var now = DateTime.UtcNow;
                now = new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
                var meta = new SaveMeta
                {
                    FormatVersion = ContainerHeader.CurrentFormatVersion,
                    GameVersion = UnityEngine.Application.version,
                    DatabaseHash = (content != null ? content.DatabaseHash : 0UL).ToString("x16", CultureInfo.InvariantCulture),
                    ProfileId = slot.Profile.Value,
                    SlotId = slot.Slot.Value,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? DefaultDisplayName(slot) : displayName,
                    Reason = reason,
                    SavedUtc = now,
                };
                context.DescribeForMeta(meta);
                // 5.
                stage = "slot thumbnail";
                diagnostic = "capture of " + stage;
                if (context.TryCaptureSlotThumbnail(out var slotThumb) && slotThumb != null && slotThumb.Rgba32 != null)
                {
                    slotThumb.Key = "slot";
                    captured.SlotThumbnail = slotThumb;
                    meta.Thumbnail = "thumbs/slot.png";
                }
                stage = "session thumbnails";
                diagnostic = "capture of " + stage;
                context.CollectThumbnails(captured.ChangedThumbnails, captured.KeepThumbnails);
                // 6.
                captured.Meta = meta;
                captured.Header = new ContainerHeader
                {
                    DatabaseHash32 = (uint)(content != null ? content.DatabaseHash : 0UL),
                    SavedUtcTicks = meta.SavedUtc.Ticks,
                    PlaytimeSeconds = meta.PlaytimeSeconds,
                };
                // 7.
                captured.Preserved.AddRange(PreservedChunks);
                captured.CaptureMs = captured.Total.Elapsed.TotalMilliseconds;
                return captured;
            }
            catch (Exception e)
            {
                captured.Release();
                GameLog.Exception("Save", e, diagnostic);
                captured.Failure = new SaveResult(false, SaveErrorKind.ParticipantFailed, SaveMessages.ParticipantFailed(stage), slot, 0, 0, 0);
                return captured;
            }
        }

        bool IsPreserved(string key)
        {
            foreach (var c in PreservedChunks) if (c.Key == key) return true;
            return false;
        }

        /// <summary>Default display names (§5.2): manual → "Save {n}", quick → "Quick Save", auto → "Autosave".</summary>
        static string DefaultDisplayName(SlotRef slot)
        {
            string id = slot.Slot.Value ?? "";
            if (id == "quick") return "Quick Save";
            if (id.StartsWith("auto_", StringComparison.Ordinal)) return "Autosave";
            var m = ManualFolder.Match(id);
            return m.Success ? "Save " + int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture) : id;
        }

        /// <summary>§5.4 steps 8–16. Thread-agnostic: no Unity API except ImageConversion.EncodeArrayToPNG. Never throws; leaves
        /// world.sav, world.bak and meta.json untouched on pre-commit failure and removes this slot's .tmp files.
        /// Metadata failure after replacement warns and returns success because the container is committed.</summary>
        internal SaveResult WriteCaptured(CapturedSave captured)
        {
            if (captured.Failure.HasValue) return captured.Failure.Value;
            var slot = captured.Slot;
            long bytesLength = 0;
            try
            {
                // 8.
                var chunks = new List<RawChunk>(captured.Participants.Count + captured.Preserved.Count + 2);
                var participantChunks = new List<RawChunk>(captured.Participants.Count);
                foreach (var (key, version, writer) in captured.Participants)
                {
                    var payload = writer.ToArray(b => { try { return b.Encode(); } finally { b.Dispose(); } });
                    writer.Reset();                                                // its blocks are disposed: forget them
                    participantChunks.Add(new RawChunk { Key = key, Version = version, Payload = payload });
                }
                captured.Release();
                // 9–11.
                captured.Meta.WorldBytes = 0;
                chunks.Add(new RawChunk { Key = "META", Version = 1, Payload = json.Serialize(captured.Meta) });
                chunks.Add(new RawChunk { Key = "IDPL", Version = 1, Payload = captured.Palette.Serialize() });
                chunks.AddRange(participantChunks);
                chunks.AddRange(captured.Preserved);
                var bytesOut = SaveContainerWriter.Write(captured.Header, chunks, config.MinCompressBytes);
                bytesLength = bytesOut.Length;

                // 12–13.
                Directory.CreateDirectory(paths.SlotDir(slot));
                string tmp = paths.TempFile(slot), world = paths.WorldFile(slot);
                if (File.Exists(tmp)) File.Delete(tmp);
                using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
                {
                    fs.Write(bytesOut, 0, bytesOut.Length);
                    fs.Flush(true);
                }
                TestHookBeforeReplace?.Invoke();
                if (File.Exists(world)) File.Replace(tmp, world, paths.BackupFile(slot), ignoreMetadataErrors: true);
                else File.Move(tmp, world);

                // 14. Thumbnail failures never fail the save.
                try { WriteThumbnails(captured); }
                catch (Exception e) { GameLog.Warn("Save", $"Thumbnails of {slot.Profile.Value}/{slot.Slot.Value} were not written: {e.Message}"); }

                // 15.
                captured.Meta.WorldBytes = bytesOut.Length;
                try { WriteReplacing(paths.MetaFile(slot), json.Serialize(captured.Meta)); }
                catch (Exception e)
                {
                    // The container is already committed. Do not report failure or roll back a good save.
                    // ListSlots detects an old sidecar against embedded META (SPEC-05 Q24).
                    DeleteTempFiles(slot);
                    GameLog.Warn("Save", $"Save committed, but metadata of {slot.Profile.Value}/{slot.Slot.Value} was not written: {e.Message}");
                }
                return new SaveResult(true, SaveErrorKind.None, IsAutosave(captured.Reason) ? SaveMessages.Autosaved : SaveMessages.Saved,
                                      slot, bytesOut.Length, captured.CaptureMs, captured.Total.Elapsed.TotalMilliseconds);
            }
            catch (Exception e)
            {
                // 16.
                captured.Release();
                DeleteTempFiles(slot);
                bool diskFull = false;
                if (e is IOException)
                {
                    try { diskFull = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(paths.Root))).AvailableFreeSpace < bytesLength + 1024 * 1024; }
                    catch (Exception) { }
                }
                return diskFull
                    ? new SaveResult(false, SaveErrorKind.DiskFull, SaveMessages.DiskFull, slot, 0, captured.CaptureMs, captured.Total.Elapsed.TotalMilliseconds)
                    : new SaveResult(false, SaveErrorKind.IoError, SaveMessages.IoError(e.Message), slot, 0, captured.CaptureMs, captured.Total.Elapsed.TotalMilliseconds);
            }
        }

        /// <summary>§5.4 step 14: the session cache follows the captured changes; files on disk follow the cache, untouched when equal.</summary>
        void WriteThumbnails(CapturedSave captured)
        {
            foreach (var t in captured.ChangedThumbnails)
                if (t?.Key != null && t.Rgba32 != null) thumbnailCache[t.Key] = EncodePng(t);
            var stale = new List<string>();
            foreach (var key in thumbnailCache.Keys) if (key != "slot" && !captured.KeepThumbnails.Contains(key)) stale.Add(key);
            foreach (var key in stale) thumbnailCache.Remove(key);
            if (captured.SlotThumbnail != null) thumbnailCache["slot"] = EncodePng(captured.SlotThumbnail);
            else thumbnailCache.Remove("slot");

            string dir = paths.ThumbsDir(captured.Slot);
            if (thumbnailCache.Count > 0) Directory.CreateDirectory(dir);
            foreach (var kv in thumbnailCache)
            {
                string file = paths.ThumbnailFile(captured.Slot, kv.Key);
                if (File.Exists(file) && new ReadOnlySpan<byte>(File.ReadAllBytes(file)).SequenceEqual(kv.Value)) continue;
                WriteReplacing(file, kv.Value);
            }
            if (Directory.Exists(dir))
                foreach (var file in Directory.GetFiles(dir, "*.png"))
                    if (!thumbnailCache.ContainsKey(Path.GetFileNameWithoutExtension(file))) File.Delete(file);
        }

        static byte[] EncodePng(ThumbnailRequest t)
            => UnityEngine.ImageConversion.EncodeArrayToPNG(t.Rgba32, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, (uint)t.Width, (uint)t.Height);

        void DeleteTempFiles(SlotRef slot)
        {
            foreach (var f in new[] { paths.TempFile(slot), paths.MetaFile(slot) + ".tmp" })
                try { if (File.Exists(f)) File.Delete(f); } catch (Exception) { }
            string dir = paths.ThumbsDir(slot);
            try { if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir, "*.tmp")) File.Delete(f); } catch (Exception) { }
        }

        /// <summary>§5.4 steps 17–18 (main thread). Blocked autosaves are the only silent outcome (decision 14).</summary>
        void FinishSave(CapturedSave captured, SaveResult result)
        {
            if (result.Success)
            {
                currentSlot = captured.Slot;
                SetProfileLastSlot(captured.Slot);
                Autosave.NotifySaved();
                EventBus<GameSaved>.Publish(new GameSaved(captured.Slot.Profile.Value, captured.Slot.Slot.Value, captured.Reason));
                GameLog.Info("Save", $"Saved {captured.Slot.Profile.Value}/{captured.Slot.Slot.Value} ({result.Bytes} bytes, capture {result.CaptureMs:F1} ms, total {result.TotalMs:F1} ms)");
                RaiseCompleted(result, captured.Reason);
                return;
            }
            if (result.Error == SaveErrorKind.Blocked)
            {
                if (RaisesWhenBlocked(captured.Reason)) RaiseCompleted(result, captured.Reason);
                return;
            }
            GameLog.Error("Save", $"Save to {captured.Slot.Profile.Value}/{captured.Slot.Slot.Value} failed: {result.Error}: {result.Message}");
            RaiseCompleted(result, captured.Reason);
        }

        void RaiseCompleted(SaveResult r, SaveReason reason)
            => OperationCompleted?.Invoke(new SaveOperationResult(false, r.Success, r.Error, r.Message, r.Slot, reason, 0, null));

        /// <summary>profile.json LastSlotId = the slot (tmp + replace); a missing or unreadable profile is left alone.</summary>
        void SetProfileLastSlot(SlotRef slot)
        {
            try
            {
                string file = paths.ProfileFile(slot.Profile);
                if (!File.Exists(file)) return;
                var d = json.Deserialize<ProfileDescriptor>(File.ReadAllBytes(file));
                d.LastSlotId = slot.Slot.Value;
                WriteReplacing(file, json.Serialize(d));
            }
            catch (Exception e) { GameLog.Warn("Save", $"Could not update profile {slot.Profile.Value}: {e.Message}"); }
        }

        // Synchronous seams for tests (InternalsVisibleTo):
        /// <summary>§5.9 quit: true approves the quit now; false starts a quit save, after which the routine quits itself.</summary>
        internal bool HandleWantsToQuit()
        {
            if (quitApproved) return true;
            if (!HasActiveSessionContext || !CanSave(out var reason) || activeProfile.Value == null)
            {
                CanSave(out reason);
                GameLog.Info("Save", "Quit without saving: " + reason);
                return true;
            }
            _ = QuitSaveAsync();
            return false;
        }

        /// <summary>Waits for the running operation, saves with reason Quit, racing QuitSaveTimeoutSeconds of real time; then quits.</summary>
        async UnityEngine.Awaitable QuitSaveAsync()
        {
            var deadline = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var save = SaveAsync(NextAutosaveSlot(activeProfile), SaveReason.Quit).GetAwaiter();   // Quit jumps the queue (§5.3)
                while (!save.IsCompleted && deadline.Elapsed.TotalSeconds < config.QuitSaveTimeoutSeconds) await UnityEngine.Awaitable.NextFrameAsync();
                if (!save.IsCompleted) GameLog.Warn("Save", $"Quit save did not finish within {config.QuitSaveTimeoutSeconds} s; quitting anyway (the previous save is intact)");
            }
            catch (Exception e) { GameLog.Exception("Save", e, "quit save"); }
            quitApproved = true;
            QuitAction?.Invoke();
        }
    }
}
