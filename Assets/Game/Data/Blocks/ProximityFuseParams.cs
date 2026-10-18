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
        // ADR-0119 (SPEC-91 §4.6), appended: what the mine does when it goes off, and seekers.
        public MinePayload Payload;     // Blast (default)
        public float PayloadStrength;   // Repulse: N·s at the centre; Emp: seconds of thruster inhibit; Gravity: GM (m³/s²) for PayloadSeconds
        public float PayloadSeconds;    // Gravity: lifetime of the temporary Attract zone
        public float SeekSpeed;         // m/s; > 0 makes an armed mine drift toward its nearest valid target
    }

    /// <summary>What a proximity fuse releases (ADR-0119). Append only.</summary>
    public enum MinePayload : byte { Blast = 0, Repulse = 1, Emp = 2, Gravity = 3, None = 4 }
}
