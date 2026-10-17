using Game.Data.Combat;

namespace Game.Data.Blocks
{
    /// <summary>Proximity fuse row (SPEC-91 §4.4): arms, senses ship roots and detonates its device. Unmanaged; Burst reads it.</summary>
    [System.Serializable] public struct ProximityFuseParams
    {
        public float ArmSeconds;        // after spawn or re-arm
        public float TriggerRadius;     // m from the fuse block centre to a ship's bounding sphere surface
        public float FuseSeconds;       // trigger → detonation
        public float BlastRadius;       // m
        public float BlastDamage;       // at the centre
        public DamageType BlastType;    // Explosive by default
        public float MinTriggerMass;    // kg; lighter roots never trigger
        public byte TriggerFilter;      // 0 = any ship, 1 = hostile to the mine's allegiance only
        public float CasingHp;          // damage taken after arming that cooks it off (0 = the first lost cell); shots below it only shove it
        public float BlastImpulse;      // N·s of push at the centre (ExplosionRequest.Knockback); 0 = the weapons' per-damage push
    }
}
