namespace Game.Core
{
    /// <summary>All randomness (architecture §1 rule 5, SPEC-02 §4.2, §5.2). Burst-compatible.
    /// <para>Rule: one stream per (purpose, logical object). A star system's generation uses For(galaxySeed, StarSystemGeneration,
    /// starSystemId.Value); a body inside it uses For(galaxySeed, BodyGeneration, StableHash.Combine(starSystemId.Value, bodySlotKey)).
    /// Destroying or adding one object never shifts another object's stream. Per-entity, per-tick randomness uses ForEntityTick.</para></summary>
    public static class RngStreams
    {
        public const uint ZeroSeedReplacement = 0x6C8E9CF5u;

        /// <summary>Never returns 0 (Unity.Mathematics.Random rejects it).</summary>
        public static uint DeriveSeed(ulong seed, RngPurpose purpose, ulong salt)
        {
            ulong h = StableHash.Combine(StableHash.Combine(seed, (ulong)purpose), salt);
            uint s = (uint)h ^ (uint)(h >> 32);
            return s != 0 ? s : ZeroSeedReplacement;
        }

        /// <summary>ArgumentException when purpose == None.</summary>
        public static Unity.Mathematics.Random For(ulong seed, RngPurpose purpose, ulong salt)
        {
            if (purpose == RngPurpose.None) throw new System.ArgumentException("RngPurpose.None is not a valid stream");
            return new Unity.Mathematics.Random(DeriveSeed(seed, purpose, salt));
        }

        /// <summary>Per-entity, per-tick stream (conventions §2.10).</summary>
        public static Unity.Mathematics.Random ForEntityTick(uint tick, int entityIndex, uint salt)
            => Unity.Mathematics.Random.CreateFromIndex((uint)StableHash.Combine(tick, (ulong)(uint)(entityIndex ^ (int)salt)));
    }
}
