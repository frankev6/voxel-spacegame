using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public struct ColliderRequest                      // 36 bytes
    {
        public Entity Grid;
        public Entity Brick;                           // may be destroyed by the time it is processed (→ "removed")
        public int3 Coord;                             // brick coordinate
        public uint Serial;                            // VoxelGrid.Serial of Grid
        public ColliderPriority Priority;
    }
}
