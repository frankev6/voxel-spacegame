using System;
using Game.Core;
using Game.Voxel.Grid;
using Game.Voxel.Model;

namespace Game.App.Session
{
    /// <summary>One owned ship and its optional registry borrow (SPEC-01 §4.3, §5.7).</summary>
    public sealed class FleetShipRecord : IDisposable
    {
        public ulong PersistentId;
        public ContentId SourceBlueprint;
        public string Name;
        public VoxelStructure Structure;
        /// <summary>ABL-006: the committed design emergency repairs rebuild towards; Structure takes the damage (write-back), this does
        /// not. Set when the ship joins the fleet or its structure is replaced by a commit; owned and disposed with the record.</summary>
        public VoxelStructure Design;
        public int StructureHandle = -1;
        VoxelStructureRegistry registry;

        internal int Register(VoxelStructureRegistry target)
        {
            if (StructureHandle >= 0) throw new InvalidOperationException("Ship structure is already registered");
            int handle = target.Register(Structure);
            registry = target; StructureHandle = handle;
            return handle;
        }
        internal void Release()
        {
            if (StructureHandle >= 0 && registry != null) registry.Release(StructureHandle);
            StructureHandle = -1; registry = null;
        }
        public void Dispose()
        {
            // A pinned source still belongs to a live spawn request; its caller must cancel/finish that request first.
            if (Structure != null && Structure.IsPinned) throw new InvalidOperationException("Cannot dispose a spawning ship");
            Release(); Structure?.Dispose(); Structure = null; Design?.Dispose(); Design = null;
        }
    }
}
