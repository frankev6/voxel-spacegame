using Unity.Entities;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponTuning.</summary>
    public struct WeaponTuning : IComponentData           // boot singleton from config_weapons (WeaponTuningDefinition, §4.7)
    {
        public int MaxProjectiles;           // 8192
        public float FixedConvergenceRad;    // radians(3)
        public float ExplosionImpulsePerDamage; // 2 N·s per damage point at the centre
        public float MaxTargetRange;         // 2500 m (TargetNext)
        public float UnderCursorRadius;      // 30 m (TargetUnderCursor)
        public int MaxCandidates;            // 32 broad-phase candidates per query
    }
}
