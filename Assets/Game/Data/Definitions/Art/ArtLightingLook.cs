namespace Game.Data.Definitions
{
    /// <summary>Scene lighting look. Implements SPEC-80 §4.1 (DESIGN-SYSTEM §5.4/§6.4/§7.4).</summary>
    [System.Serializable]
    public struct ArtLightingLook
    {
        public float keyIntensity;                 // DRYDOCK 2.0, NOCTURNE 1.6, PULP 1.8
        public float keyPitchDegrees;              // 35 / 25 / 45 (rule R4)
        public float keySaturation;                // 0..1 of the spectral colour: 0.6 / 1.0 / 0.3
        public UnityEngine.Color fillColor;        // fill (or rim) light colour, sRGB
        public float fillIntensity;                // 0.3 / 0.56 / 0.54
        public float fillPitchDegrees;             // 50 / 20 / 45
        public bool fillFromCameraForward;         // false = opposite the key azimuth; true = camera-forward rim (NOCTURNE)
        public UnityEngine.Color ambientSky, ambientEquator, ambientGround; // flat ambient = three equal colours
        public int reflectionCubemapSize;          // 128
    }
}
