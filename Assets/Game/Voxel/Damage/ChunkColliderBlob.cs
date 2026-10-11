using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Owned collider of a chunk body; disposed through DeferredDisposalSystem.</summary>
    public struct ChunkColliderBlob : ICleanupComponentData { public BlobAssetReference<Unity.Physics.Collider> Value; }
}
