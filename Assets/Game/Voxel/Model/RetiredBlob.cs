using Unity.Entities;
using Unity.Physics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. A collider blob waiting for deferred disposal (§5.10).</summary>
    public struct RetiredBlob { public BlobAssetReference<Collider> Blob; public uint DueFrame; public uint DueTick; }
}
