using Unity.Entities;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>ADR-0055: owns a split body's provisional collider until the normal body cache adopts it.</summary>
    public struct GridUnadoptedCollider : ICleanupComponentData { public BlobAssetReference<Collider> Value; }
}
