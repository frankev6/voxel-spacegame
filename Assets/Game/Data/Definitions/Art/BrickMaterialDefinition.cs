namespace Game.Data.Definitions
{
    /// <summary>All brick looks of one art direction; baked into M_Brick. Implements SPEC-80 §4.2.</summary>
    [ContentIdPrefix("brickmat_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Art/Brick Material", fileName = "brickmat_new")]
    public sealed class BrickMaterialDefinition : ContentDefinition
    {
        public const int LayerCount = 64;
        public const int LayerSize = 256;
        public const int MissingLayer = 63;                   // SPEC-04 block_missing

        [UnityEngine.SerializeField] private BrickLayer[] layers = new BrickLayer[LayerCount];
        [UnityEngine.SerializeField] private UnityEngine.Texture2D crackTexture;      // 256² R8
        [UnityEngine.SerializeField] private UnityEngine.Texture2D damageRamp;        // 256×1 sRGB
        [UnityEngine.SerializeField] private UnityEngine.Texture2D macroNoise;        // 256² R8
        [UnityEngine.SerializeField] private UnityEngine.Texture2D toonRamp;          // 256×1 (may be null when shadingMode = 0)
        [UnityEngine.SerializeField] private BrickShaderParams shaderParams;

        [UnityEngine.Header("Baked by BrickMaterialBaker (ART-007). Do not edit by hand.")]
        [UnityEngine.SerializeField] private UnityEngine.Texture2DArray albedoArray;
        [UnityEngine.SerializeField] private UnityEngine.Texture2DArray maskArray;
        [UnityEngine.SerializeField] private UnityEngine.Texture2DArray normalArray;
        [UnityEngine.SerializeField] private UnityEngine.Texture2D layerParams;       // 64×2 RGBAHalf
        [UnityEngine.SerializeField] private UnityEngine.Texture2D paintPaletteTexture; // 256×1 RGBA8 sRGB
        [UnityEngine.SerializeField] private UnityEngine.Material material;           // M_Brick.mat

        public System.Collections.Generic.IReadOnlyList<BrickLayer> Layers => layers;
        public UnityEngine.Texture2D CrackTexture => crackTexture;
        public UnityEngine.Texture2D DamageRamp => damageRamp;
        public UnityEngine.Texture2D MacroNoise => macroNoise;
        public UnityEngine.Texture2D ToonRamp => toonRamp;
        public BrickShaderParams ShaderParams => shaderParams;
        public UnityEngine.Texture2DArray AlbedoArray => albedoArray;
        public UnityEngine.Texture2DArray MaskArray => maskArray;
        public UnityEngine.Texture2DArray NormalArray => normalArray;
        public UnityEngine.Texture2D LayerParams => layerParams;
        public UnityEngine.Texture2D PaintPaletteTexture => paintPaletteTexture;
        public UnityEngine.Material Material => material;
    }
}
