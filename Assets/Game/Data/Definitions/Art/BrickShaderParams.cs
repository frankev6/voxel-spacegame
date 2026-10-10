namespace Game.Data.Definitions
{
    /// <summary>Scalar brick shader parameters (DESIGN-SYSTEM §10.14.3). Implements SPEC-80 §4.2.</summary>
    [System.Serializable]
    public struct BrickShaderParams
    {
        public float crackTiling;
        public UnityEngine.Color scorchColor; public float scorchStrength;
        [UnityEngine.ColorUsage(false, true)] public UnityEngine.Color emberColor;
        public float emberIntensity, emberFadeSeconds;
        public UnityEngine.Vector4 exposedLayers;             // x, y, z = layer indices, w = count
        public float aoStrength; public UnityEngine.Color aoColor;
        public float macroScale, macroStrength;
        public float emissiveIntensity;
        public float seamEmission, seamFadeStart, seamFadeEnd;
        public UnityEngine.Color rimColor; public float rimStrength;
        public float shadingMode;                             // 0 PBR, 1 toon ramp
        public float halftoneScale;
    }
}
