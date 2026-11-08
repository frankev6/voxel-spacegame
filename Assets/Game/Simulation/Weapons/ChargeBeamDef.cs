using Game.Core;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §4.6d (WPN-019): baked ChargeBeamData, one row per WeaponDef index in WeaponDefsBlob.ChargeBeams.
    /// ChargeTicks == 0 means the weapon is not a charge beam.</summary>
    public struct ChargeBeamDef
    {
        public uint ChargeTicks;             // ≥ 1 for a charge beam
        public float TrackRateRad;           // per second
        public float ConvergeRadius;         // m
        public float ConvergeBeamWidth;      // m
        public float ConvergedHoldFraction;  // 0..0.9
        public int ConvergeBeams;            // 2..12
        public ContentHash FireCue;          // played when the main beam fires; 0 = silent
    }
}
