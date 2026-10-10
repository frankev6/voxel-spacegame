namespace Game.Data.Definitions
{
    /// <summary>Post-processing look. Implements SPEC-80 §4.1.</summary>
    [System.Serializable]
    public struct ArtPostLook
    {
        public bool acesTonemapping;               // false = Neutral
        public float bloomThreshold, bloomIntensity, bloomScatter;
        public float vignette, filmGrain;          // 0..1
        public float saturation, contrast;         // URP Color Adjustments units (−100..100)
    }
}
