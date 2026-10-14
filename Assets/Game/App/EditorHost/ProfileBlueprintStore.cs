using System;
using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Data;
using Game.Voxel.Model;
using Unity.Collections;

namespace Game.App.EditorHost
{
    /// <summary>SPEC-51 EDT-020: one profile's standalone VOXS files plus immutable catalog blueprints.</summary>
    internal sealed class ProfileBlueprintStore : IBlueprintStore
    {
        readonly string directory;
        readonly ContentCatalog catalog;
        readonly EditorContext context;
        readonly VoxsBlockNames names;
        readonly Dictionary<string, ShipBlueprintDefinition> content = new(StringComparer.Ordinal);
        readonly HashSet<string> warned = new(StringComparer.Ordinal);
        internal ProfileBlueprintStore(SavePaths paths, ProfileId profile, ContentCatalog catalog, EditorContext context)
        {
            if (paths == null) throw new ArgumentNullException(nameof(paths));
            // Profile IDs come from SaveService; reject path-bearing values before resolving any files.
            if (profile.Value == null || profile.Value.Length < 4 || profile.Value[0] != 'p') throw new ArgumentException("A profile ID is required.", nameof(profile));
            for (int i = 1; i < profile.Value.Length; i++) if (profile.Value[i] < '0' || profile.Value[i] > '9') throw new ArgumentException("Invalid profile ID.", nameof(profile));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            directory = Path.Combine(paths.ProfileDir(profile), "blueprints"); names = VoxsBlockNames.FromCatalog(catalog);
            foreach (var definition in catalog.All<ShipBlueprintDefinition>()) content.Add(definition.IdString, definition);
        }
        public void List(List<BlueprintInfo> result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            result.Clear();
            foreach (var pair in content)
            {
                if (!TryLoad(pair.Key, out var structure, out var error)) { Warn(pair.Key, error); continue; }
                using (structure) result.Add(new BlueprintInfo { Id = pair.Key, DisplayName = string.IsNullOrEmpty(pair.Value.DisplayName) ? pair.Key : pair.Value.DisplayName,
                    ReadOnly = true, BlockCount = Count(structure) });
            }
            if (Directory.Exists(directory)) foreach (string file in Directory.GetFiles(directory, "*.ssbp"))
            {
                string id = Path.GetFileNameWithoutExtension(file);
                if (!BlueprintNames.IsId(id) || content.ContainsKey(id)) continue;
                if (!TryLoad(id, out var structure, out var error)) { Warn(id, error); continue; }
                using (structure) result.Add(new BlueprintInfo { Id = id, DisplayName = id, BlockCount = Count(structure), ModifiedUtcTicks = File.GetLastWriteTimeUtc(file).Ticks });
            }
            result.Sort((a, b) => { int order = StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName, b.DisplayName); return order != 0 ? order : StringComparer.Ordinal.Compare(a.Id, b.Id); });
        }
        int Count(VoxelStructure structure)
        {
            using var records = new NativeList<BlockRecord>(Allocator.TempJob);
            new DraftReader(structure, context).Enumerate(records); return records.Length;
        }
        void Warn(string id, string error) { if (warned.Add(id)) GameLog.Warn("EDT", "Cannot list blueprint " + id + ": " + error); }
        string FileFor(string id) => Path.Combine(directory, id + ".ssbp");
        public bool TryLoad(string blueprintId, out VoxelStructure structure, out string error)
        {
            structure = null; error = null;
            try
            {
                if (blueprintId != null && content.TryGetValue(blueprintId, out var definition))
                {
                    if (!VoxelStructureCodec.TryDecode(definition.Payload, catalog, out structure, out error)) return false;
                    structure.SetReadOnly(); return true;
                }
                if (!BlueprintNames.IsId(blueprintId)) { error = "Invalid blueprint ID."; return false; }
                string file = FileFor(blueprintId);
                if (!File.Exists(file)) { error = "Blueprint not found."; return false; }
                return VoxelStructureCodec.TryDecode(File.ReadAllBytes(file), catalog, out structure, out error);
            }
            catch (Exception e) when (IsStorageError(e)) { structure?.Dispose(); structure = null; error = e.Message; return false; }
        }
        public bool TrySave(string displayName, VoxelStructure structure, out string blueprintId, out string error)
        {
            blueprintId = null; error = null; string temporary = null; bool ownsTemporary = false;
            try
            {
                if (structure == null) { error = "Blueprint structure is missing."; return false; }
                string baseId = BlueprintNames.Sanitize(displayName);
                if (content.ContainsKey(baseId)) { error = "Content blueprints are read-only."; return false; }
                // Whole-design validation belongs to the editor's EDT-009/022 save command. This owner validates the codec and storage.
                byte[] bytes = VoxelStructureCodec.Encode(structure, names, VoxsEncodeOptions.ForAsset);
                Directory.CreateDirectory(directory);
                string id = BlueprintNames.Unique(baseId, candidate => content.ContainsKey(candidate) || File.Exists(FileFor(candidate)) || File.Exists(FileFor(candidate) + ".tmp"));
                string destination = FileFor(id); temporary = destination + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { ownsTemporary = true; stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                File.Move(temporary, destination); temporary = null; blueprintId = id; return true;
            }
            catch (Exception e) when (IsStorageError(e)) { error = e.Message; return false; }
            finally
            {
                if (ownsTemporary && temporary != null) try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        public bool TryDelete(string blueprintId, out string error)
        {
            error = null;
            if (blueprintId != null && content.ContainsKey(blueprintId)) { error = "Content blueprints are read-only."; return false; }
            if (!BlueprintNames.IsId(blueprintId)) { error = "Invalid blueprint ID."; return false; }
            try
            {
                string file = FileFor(blueprintId); if (!File.Exists(file)) { error = "Blueprint not found."; return false; }
                File.Delete(file); return true;
            }
            catch (Exception e) when (IsStorageError(e)) { error = e.Message; return false; }
        }
        static bool IsStorageError(Exception e) => e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is SaveFormatException;
    }
}
