namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-63 §4; shared content boundary per ADR-0050.</summary>
    [System.Serializable]
    public struct FxQualityBudget
    {
        public int DebrisLiveCap;        // max live debris particles (estimate, §5.2 step 9)
        public int DebrisSpawnPerFrame;  // max new debris particles per frame
        public float ParticleFactor;     // multiplies FxDefinition.particlesPerEvent (result clamped to >= 1)
        public int MaxTracers, MaxBeams, MaxPlumes, MaxShields;
    }
}
