using Game.Data.Blocks;
using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5.</summary>
    public struct BrickDirty : IComponentData, IEnableableComponent
    {
        public BrickDirtyFlags Flags;
        public BlockFaceMask BorderMask;   // brick faces whose border layer changed → face neighbours need remeshing (SPEC-04 mask)
    }
}
