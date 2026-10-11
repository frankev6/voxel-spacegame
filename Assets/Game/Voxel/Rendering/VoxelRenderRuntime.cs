using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct VoxelRenderRuntime : IComponentData
    {
        public BatchMaterialID BrickMaterial, PropMaterial;
        public BatchMeshID PlaceholderMesh;
        public UnityObjectRef<Mesh> Placeholder;
        public NativeArray<BatchMeshID> PropMeshIds;
        public NativeArray<AABB> PropMeshBounds;
        public BlobAssetReference<BrickShapeTables> Shapes;
        public UnityObjectRef<VoxelRenderManagedState> Managed;
        public byte IsReady;
        public static VoxelRenderRuntime Create() => default;
        public void Dispose()
        {
            if (PropMeshIds.IsCreated) PropMeshIds.Dispose();
            if (PropMeshBounds.IsCreated) PropMeshBounds.Dispose();
            if (Shapes.IsCreated) Shapes.Dispose();
        }
    }
}
