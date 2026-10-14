using System;
using System.Collections.Generic;
using System.IO;
using Game.Data.Services;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Voxel.Model;

namespace Game.App.EditorHost
{
    /// <summary>App-lifetime context with storage resolved against the current SaveService profile.</summary>
    internal sealed class ActiveProfileBlueprintStore : IBlueprintStore, IDisposable
    {
        readonly SaveService saves;
        readonly SavePaths paths;
        readonly ContentService content;
        readonly EditorContext context;
        ProfileBlueprintStore store;
        ProfileId profile;
        bool disposed;
        internal ActiveProfileBlueprintStore(SaveService saves, SavePaths paths, ContentService content, ShipEditorConfig config)
        {
            this.saves = saves; this.paths = paths; this.content = content;
            context = new EditorContext(config, content.Catalog, content.BlockDefs, content.Orientations, 16);
        }
        ProfileBlueprintStore Current()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ActiveProfileBlueprintStore));
            var active = saves.ActiveProfile;
            if (string.IsNullOrEmpty(active.Value)) { active = saves.CreateProfile("Blueprints").Id; saves.SetActiveProfile(active); }
            if (store == null || !profile.Equals(active))
            { profile = active; store = new ProfileBlueprintStore(paths, active, content.Catalog, context); }
            return store;
        }
        public void List(List<BlueprintInfo> result) => Current().List(result);
        public bool TryLoad(string id, out VoxelStructure structure, out string error)
        {
            try { return Current().TryLoad(id, out structure, out error); }
            catch (Exception e) when (StorageFailure(e)) { structure = null; error = e.Message; return false; }
        }
        public bool TrySave(string name, VoxelStructure structure, out string id, out string error)
        {
            try { return Current().TrySave(name, structure, out id, out error); }
            catch (Exception e) when (StorageFailure(e)) { id = null; error = e.Message; return false; }
        }
        public bool TryDelete(string id, out string error)
        {
            try { return Current().TryDelete(id, out error); }
            catch (Exception e) when (StorageFailure(e)) { error = e.Message; return false; }
        }
        static bool StorageFailure(Exception error) => error is IOException || error is UnauthorizedAccessException;
        public void Dispose() { if (disposed) return; disposed = true; store = null; context.Dispose(); }
    }
}
