using Game.Data.Blocks;
using Unity.Entities;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. The two SPEC-04 blobs, passed together.</summary>
    public struct VoxelContent
    { public BlobAssetReference<BlockDefsBlob> Blocks; public BlobAssetReference<OrientationTablesBlob> Orientations; }
}
