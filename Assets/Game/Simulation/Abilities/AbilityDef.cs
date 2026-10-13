using Game.Core;
using Game.Data.Definitions;
using Unity.Entities;

namespace Game.Simulation.Abilities
{
    /// <summary>Implements SPEC-24 §4.2. Baked ability; index 0 = none. Times in ticks.</summary>
    public struct AbilityDef
    {
        public AbilityKind Kind;
        public byte MaxCharges;
        public ushort Reserved;
        public float EnergyCost;
        public float CooldownTicks;          // cooldownSeconds × 60
        public float ChargeRechargeTicks;    // chargeRechargeSeconds × 60
        public uint DurationTicks;           // SimTime.SecondsToTicks(durationSeconds)
        public float ShieldEnableThreshold;
        public BlobArray<AbilityStatModDef> Mods;
        public ContentHash IdHash, ActivationCue, ActivationFx;
    }
}
