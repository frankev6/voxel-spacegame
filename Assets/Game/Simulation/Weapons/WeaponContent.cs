using System;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Weapons
{
    /// <summary>SPEC-21 §5.1: immutable, catalog-ordered weapon data.</summary>
    public static class WeaponContent
    {
        public static void CreateSingletons(EntityManager em, ContentCatalog catalog, System.Collections.Generic.List<Entity> created)
        {
            created.Add(em.CreateSingleton(new WeaponDefsRef { Value = BuildBlob(catalog) }));
            var definition = catalog.Get<WeaponTuningDefinition>(new ContentId("config_weapons"));
            created.Add(em.CreateSingleton(new WeaponTuning { MaxProjectiles = definition.MaxProjectiles,
                FixedConvergenceRad = math.radians(definition.FixedConvergenceDeg), ExplosionImpulsePerDamage = definition.ExplosionImpulsePerDamage,
                MaxTargetRange = definition.MaxTargetRange, UnderCursorRadius = definition.UnderCursorRadius, MaxCandidates = definition.MaxCandidates }));
            // TODO(WPN-002): faction relations are required when targeting/autofire is enabled.
        }
        public static BlobAssetReference<WeaponDefsBlob> BuildBlob(ContentCatalog catalog)
        {
            var definitions = catalog.All<WeaponDefinition>();
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WeaponDefsBlob>();
            var weapons = builder.Allocate(ref root.Weapons, definitions.Count + 1);
            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i]; UnityEngine.Color32 color = d.TracerColor;
                var w = new WeaponDef
                {
                    Index = (ushort)(i + 1), Type = d.DamageType, IsFixed = (byte)(d.TraverseHalfArcDeg == 0 ? 1 : 0),
                    DefaultFireGroups = (byte)d.DefaultFireGroups, ExplodeOnExpire = (byte)(d.ExplodeOnExpire ? 1 : 0),
                    TriggerIntervalTicks = 60f / d.FireRate, BurstIntervalTicks = d.BurstInterval * 60f,
                    LifetimeMinTicks = math.max(1u, SimTime.SecondsToTicks(d.LifetimeMin)), LifetimeMaxTicks = math.max(1u, SimTime.SecondsToTicks(d.LifetimeMax)),
                    BeamPulseTicks = math.max(1u, SimTime.SecondsToTicks(d.BeamPulseSeconds)),
                    FireCue = d.FireCue != null ? d.FireCue.Id.Hash : default, MuzzleFx = d.MuzzleFx != null ? d.MuzzleFx.Id.Hash : default,
                    HitFx = d.HitFx != null ? d.HitFx.Id.Hash : default, IdHash = d.Id.Hash,
                    TracerColor = (uint)(color.r | color.g << 8 | color.b << 16 | color.a << 24),
                    Delivery = d.Delivery,
                    Visual = d.Visual,
                    Damage = d.Damage,
                    PenetrationCells = d.PenetrationCells,
                    ExplosionRadius = d.ExplosionRadius,
                    ExplosionDamage = d.ExplosionDamage,
                    ImpulsePerHit = d.ImpulsePerHit,
                    BurstCount = d.BurstCount,
                    PelletsPerRound = d.PelletsPerRound,
                    ProjectileSpeed = d.ProjectileSpeed,
                    ProjectileRadius = d.ProjectileRadius,
                    BeamRange = d.BeamRange,
                    BeamWidth = d.BeamWidth,
                    PowerDraw = d.PowerDraw,
                    SpreadRad = math.radians(d.SpreadDeg),
                    HomingTurnRateRad = math.radians(d.HomingTurnRateDeg),
                    TraverseHalfArcRad = math.radians(d.TraverseHalfArcDeg),
                    TraverseRateRad = math.radians(d.TraverseRateDeg),
                    MaxElevationRad = math.radians(d.MaxElevationDeg),
                    FireConeRad = math.radians(d.FireConeDeg),
                };
                w.Range = w.Delivery == WeaponDelivery.Beam ? w.BeamRange : d.ProjectileSpeed * d.LifetimeMax;
                w.SustainedDps = ComputeSustainedDps(w); weapons[i + 1] = w;
            }
            return builder.CreateBlobAssetReference<WeaponDefsBlob>(Allocator.Persistent);
        }
        public static float ComputeSustainedDps(in WeaponDef w) => (w.Delivery == WeaponDelivery.Beam ? w.Damage :
            (w.Damage + w.ExplosionDamage * .5f) * w.PelletsPerRound * w.BurstCount) * (60f / w.TriggerIntervalTicks);
    }
}
