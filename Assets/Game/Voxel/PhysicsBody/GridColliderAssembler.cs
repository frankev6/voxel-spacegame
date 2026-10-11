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
    public static class GridColliderAssembler
    {
        /// children must be sorted by (Coord.z, Coord.y, Coord.x) and non-empty; returns a new Persistent CompoundCollider blob.
        public static BlobAssetReference<Collider> Assemble(NativeArray<BrickChild> children, float cellSize) => throw new System.NotImplementedException("PHY-005");
        public static int CompareCoords(int3 a, int3 b) => throw new System.NotImplementedException("PHY-005");   // z, then y, then x; -1/0/1
    }
}
