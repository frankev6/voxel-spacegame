using Game.Core;
using Game.Data.Combat;
using Game.Data.Definitions;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: WeaponDef.</summary>
    public struct WeaponDef
    {
        public ushort Index;
        public WeaponDelivery Delivery;
        public DamageType Type;
        public ProjectileVisual Visual;
        public byte DefaultFireGroups;       // WeaponFireGroups bits
        public byte ExplodeOnExpire;
        public byte IsFixed;                 // TraverseHalfArc == 0
        public float Damage, PenetrationCells, ExplosionRadius, ExplosionDamage, ImpulsePerHit;
        public float TriggerIntervalTicks;   // 60 / fireRate
        public int BurstCount;
        public float BurstIntervalTicks;     // burstInterval × 60
        public int PelletsPerRound;
        public float SpreadRad;
        public float ProjectileSpeed, ProjectileRadius;
        public uint LifetimeMinTicks, LifetimeMaxTicks;   // SimTime.SecondsToTicks, ≥ 1
        public float HomingTurnRateRad;      // per second
        public float BeamRange;
        public uint BeamPulseTicks;          // ≥ 1
        public float BeamWidth;
        public float TraverseHalfArcRad, TraverseRateRad, MaxElevationRad, FireConeRad;
        public float PowerDraw;              // E/s (SPEC-22 reads)
        public float Range;                  // m: projectile/missile speed × lifetimeMax; beam BeamRange (SPEC-22, SPEC-23 read)
        public float SustainedDps;           // §4.9 formula (SPEC-22 reads)
        public ContentHash FireCue, MuzzleFx, HitFx, IdHash;
        public uint TracerColor;             // sRGB RGBA8 packed R | G<<8 | B<<16 | A<<24
    }
}
