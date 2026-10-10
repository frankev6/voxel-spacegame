namespace Game.Core
{
    /// <summary>All randomness (architecture §1 rule 5, SPEC-02 §4.2, §5.2).</summary>
    public static class RngStreams
    {
        public const uint ZeroSeedReplacement = 0x6C8E9CF5u;
        public static uint DeriveSeed(ulong seed, RngPurpose purpose, ulong salt) { throw new System.NotImplementedException("CORE-003"); }                      // Burst-compatible, never returns 0
        public static Unity.Mathematics.Random For(ulong seed, RngPurpose purpose, ulong salt) { throw new System.NotImplementedException("CORE-003"); }         // ArgumentException when purpose == None
        public static Unity.Mathematics.Random ForEntityTick(uint tick, int entityIndex, uint salt) { throw new System.NotImplementedException("CORE-003"); }   // per-entity, per-tick (conventions §2.10)
    }
}
