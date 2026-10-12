using Unity.Entities;
using Unity.Mathematics;
using Game.Data.Definitions;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponShot.</summary>
    public struct WeaponShot
    {
        public Entity Weapon, Grid, Instigator, Target;   // Target = ShipTarget for homing deliveries, else Null
        public float3 MuzzleWorld;      // local frame
        public float3 DirWorld;         // unit; spread already applied per pellet
        public float3 ShooterVelocity;  // PhysicsVelocity.Linear of the root
        public uint ShotId;             // §5.5 step 6 (pellets: ShotId + k)
        public ushort Def;              // WeaponDef index
        public WeaponDelivery Delivery;
        public ProjectileFlags Flags;   // PlayerSource etc.
        public float DamageMultiplier;
    }
}
