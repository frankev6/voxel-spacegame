namespace Game.Data.Blocks
{
    /// <summary>One block's editor stats at one orientation, summed by BlockStatMath (SPEC-51 preview, SPEC-61 fleet screen). Add fields at the end only.
    /// Implements SPEC-04 §4.6.</summary>
    public struct BlockStatContribution
    {
        public float Mass;                         // kg = cells × cellMass (BlockStatMath fills it; modules do not)
        public Unity.Mathematics.float3 Thrust;    // N, grid space at the given orientation
        public float PowerGeneration;              // E/s
        public float PowerDraw;                    // E/s at full demand
        public float PowerStorage;                 // E
        public float SustainedDps;
        public float ShieldCapacity, ShieldRegen;  // HP, HP/s
        public float CargoUnits;
    }
}
