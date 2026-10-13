using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.5. ECS singleton baked from PowerTuningDefinition by PowerContent. Read-only at runtime.</summary>
    public struct PowerTuning : IComponentData
    {
        public byte PriorityShieldEmitter, PriorityThruster, PriorityAbility, PriorityWeapon;
        public int BrownoutHoldTicks;
        public float ShieldPaddingScale, ShieldPaddingMetres, ShieldMinRadiusMetres, ShieldRestartFraction;
        // Per-type shield multipliers moved to SPEC-13's DamageTableBlob (DamageTypeRow.ShieldMultiplier, review X10).
        public float SustainWarningSeconds;
        public static PowerTuning Defaults => new PowerTuning { PriorityShieldEmitter = 200, PriorityThruster = 150, PriorityAbility = 120,
            PriorityWeapon = 100, BrownoutHoldTicks = 30, ShieldPaddingScale = 1.2f, ShieldPaddingMetres = 2f, ShieldMinRadiusMetres = 3f,
            ShieldRestartFraction = 0.25f, SustainWarningSeconds = 30f };
    }
}
