namespace Game.Data.Definitions
{
    /// <summary>One slice of the brick texture arrays (index = BlockDefinition.materialLayer). Implements SPEC-80 §4.2.</summary>
    [System.Serializable]
    public struct BrickLayer
    {
        public string name;                                   // DESIGN-SYSTEM §10.12 name, e.g. "hull_plain"
        public UnityEngine.Texture2D albedo;                  // 256×256, sRGB
        public UnityEngine.Texture2D mask;                    // 256×256, linear: R metallic, G smoothness, B paint mask, A emissive mask
        public UnityEngine.Texture2D normal;                  // 256×256, normal map
        public int copyOf;                                    // −1 = own textures; else index of the layer copied ("→ n")
        [UnityEngine.ColorUsage(false, true)] public UnityEngine.Color emissive;   // linear HDR
        public BrickEmissiveMode emissiveMode;
        [UnityEngine.Range(1, 2)] public int macroTile;       // 1 or 2 cells
        public bool scorchable;
        [UnityEngine.Range(0f, 1f)] public float crackStrength;
        [UnityEngine.Range(0f, 1f)] public float edgeWear;
    }
}
