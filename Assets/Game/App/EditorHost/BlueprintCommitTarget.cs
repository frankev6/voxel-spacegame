using System;
using Game.ShipEditor.Services;
using Game.Voxel.Model;

namespace Game.App.EditorHost
{
    /// <summary>The store borrows its input; a successful editor transfer is disposed after serialization.</summary>
    public sealed class BlueprintCommitTarget : IEditCommitTarget
    {
        readonly IBlueprintStore store;
        public string Name { get; set; }
        public string BlueprintId { get; private set; }
        public BlueprintCommitTarget(IBlueprintStore store, string name)
        { this.store = store ?? throw new ArgumentNullException(nameof(store)); Name = name; }
        public bool TryCommitStructure(VoxelStructure committed, out string error)
        {
            error = null;
            try
            {
                if (!store.TrySave(Name, committed, out var id, out error)) return false;
                BlueprintId = id;
                committed.Dispose();
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }
    }
}
