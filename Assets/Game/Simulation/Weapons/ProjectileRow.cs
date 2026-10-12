using Unity.Entities;
using Unity.Mathematics;
using Game.Data.Combat;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: ProjectileRow.</summary>
    public struct ProjectileRow
    {
        public float3 Position;          // local frame, end of the last step
        public float3 Velocity;          // m/s
        public Entity Source;            // shooter grid root
        public Entity Target;            // homing target root or Entity.Null
        public uint ShotId;              // (instance index & 0xFFFF) << 16 | (ShotCounter & 0xFFFF)
        public uint DeathTick;           // SimClock.Tick at which it expires
        public ushort Weapon;            // WeaponDef index
        public DamageType Type;
        public ProjectileFlags Flags;
        public float DamageMultiplier;   // shooter ShipStatMultipliers.Get(StatId.WeaponDamage) at fire time (1 without SPEC-22)
        public Entity Instigator;        // review X6: OwnedBy.Owner of Source, else Source (resolved at fire time)
    }
}
