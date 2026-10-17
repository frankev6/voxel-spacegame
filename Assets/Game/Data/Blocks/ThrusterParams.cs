namespace Game.Data.Blocks
{
    /// <summary>Thruster row: thrust along block-local −Z, power draw at full throttle, and how the engine responds (ADR-0120).
    /// Unmanaged; Burst reads it. Implements SPEC-04 §4.6 and SPEC-20 FLT-014.</summary>
    [System.Serializable] public struct ThrusterParams
    {
        public float ThrustNewtons;      // > 0 along block-local −Z
        public float PowerDraw;          // E/s at full throttle, ≥ 0
        public float SpoolUpSeconds;     // ≥ 0: time from idle to full throttle; 0 = instant (ADR-0120)
        public float SpoolDownSeconds;   // ≥ 0: time from full throttle to idle; 0 = instant
        public float BoostThrottle;      // ≥ 0: throttle ceiling while boosting; 0 = FlightConfig.BoostThrottle
    }
}
