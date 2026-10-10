namespace Game.Core
{
    /// <summary>Fixed-step constants (architecture §2). Implements SPEC-02 §4.2.</summary>
    public static class SimTime
    {
        public const int TicksPerSecond = 60;
        public const float FixedTimestep = 1f / 60f;
        public const double FixedTimestepD = 1.0 / 60.0;
        public const int MaxTicksPerFrame = 6;                          // Time.maximumDeltaTime = 0.1 (architecture §5.1)
        public static uint SecondsToTicks(float seconds) { throw new System.NotImplementedException("CORE-003"); }              // round-half-up of seconds*60, negative → 0. Burst-compatible
        public static double TicksToSeconds(uint ticks) => ticks * FixedTimestepD;
    }
}
