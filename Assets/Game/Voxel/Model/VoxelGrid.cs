using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5 (architecture §6.3). On the root.</summary>
    public struct VoxelGrid : IComponentData
    {
        public int3 BrickMin, BrickDims;   // BrickSlot table bounds; BrickDims product ≤ 262,144
        public float CellSize;
        public uint Revision;              // writers: DamageResolveSystem, SplitSystem, GridSpawnSystem
        public int AnchorInstance;         // core instance index or -1 (largest island anchors); written at spawn, afterwards by SPEC-13
        public GridKind Kind;              // written at spawn, afterwards by ShipDeathSystem (SPEC-13)
        public uint Serial;                // session-unique creation number ≥ 1; deterministic ordering and SPEC-12 cache key
        public int3 CellMin, CellMax;      // inclusive solid bounds at spawn/rebuild; never shrunk at runtime (conservative)
    }
}
