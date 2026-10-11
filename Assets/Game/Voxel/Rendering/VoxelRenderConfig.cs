using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct VoxelRenderConfig : IComponentData
    {
        public UnityObjectRef<Material> BrickMaterial, PropMaterial;
        public UnityObjectRef<Texture2D> Palette;
        public NativeArray<UnityObjectRef<Mesh>> PropMeshes;
        public int MeshBatchMin, MeshBatchMax;
        public float MeshApplyBudgetMs, Lod1Distance, Lod2Distance, LodHysteresis, DormantDistance;
        public byte ReleaseCpuMeshCopies;
    }
}
