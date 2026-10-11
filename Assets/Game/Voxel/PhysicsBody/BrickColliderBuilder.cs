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
    public static class BrickColliderBuilder
    {
        /// Null for an empty mask. One box → a BoxCollider blob; more → a CompoundCollider of boxes (child blobs disposed).
        public static BlobAssetReference<Collider> Build(in BrickMaskBits mask, float cellSize, int maxBoxes, float bevelFraction,
                                                         CollisionFilter filter, Material material,
                                                         ref NativeList<BrickBox> scratch, out int boxCount) => throw new System.NotImplementedException("PHY-005");
    }
}
