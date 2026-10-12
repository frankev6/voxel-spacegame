using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    [ContentIdPrefix("config_")]
    public sealed class VoxelRenderConfigDefinition : ContentDefinition
    {
        [SerializeField] private Material brickMaterial;
        [SerializeField] private Material propMaterial;
        [SerializeField] private ComputeShader brickPoolCopy;
        [SerializeField] private VoxelPaletteDefinition palette;
        [SerializeField, Range(16, 128)] private int meshBatchMin = 16;
        [SerializeField, Range(16, 256)] private int meshBatchMax = 128;
        [SerializeField, Min(0.25f)] private float meshApplyBudgetMs = 2.0f;
        [SerializeField, Min(50f)] private float lod1Distance = 400f;
        [SerializeField, Min(100f)] private float lod2Distance = 2000f;
        [SerializeField, Range(0f, 0.5f)] private float lodHysteresis = 0.1f;
        [SerializeField, Min(500f)] private float dormantDistance = 3000f;
        [SerializeField] private bool releaseCpuMeshCopies = false;
        public Material BrickMaterial => brickMaterial;
        public Material PropMaterial => propMaterial;
        public ComputeShader BrickPoolCopy => brickPoolCopy;
        public VoxelPaletteDefinition Palette => palette;
        public int MeshBatchMin => meshBatchMin;
        public int MeshBatchMax => meshBatchMax;
        public float MeshApplyBudgetMs => meshApplyBudgetMs;
        public float Lod1Distance => lod1Distance;
        public float Lod2Distance => lod2Distance;
        public float LodHysteresis => lodHysteresis;
        public float DormantDistance => dormantDistance;
        public bool ReleaseCpuMeshCopies => releaseCpuMeshCopies;
    }
}
