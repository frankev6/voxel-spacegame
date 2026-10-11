namespace Game.Core
{
    /// <summary>Fixed-step constants (architecture §2). Implements SPEC-02 §4.2.</summary>
    public static class SimTime
    {
        public const int TicksPerSecond = 60;
        public const float FixedTimestep = 1f / 60f;
        public const double FixedTimestepD = 1.0 / 60.0;
        public const int MaxTicksPerFrame = 6;                          // Time.maximumDeltaTime = 0.1 (architecture §5.1)
        /// <summary>Round-half-up of seconds·60; negative or NaN → 0; above uint.MaxValue/60 s → uint.MaxValue. Burst-compatible.</summary>
        public static uint SecondsToTicks(float seconds)
        {
            if (!(seconds > 0f)) return 0;
            if (seconds >= uint.MaxValue / (float)TicksPerSecond) return uint.MaxValue;
            return (uint)Unity.Mathematics.math.floor(seconds * 60f + 0.5f);
        }
        public static double TicksToSeconds(uint ticks) => ticks * FixedTimestepD;
    }
}
