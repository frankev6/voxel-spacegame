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
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (created == null) throw new ArgumentNullException(nameof(created));
            using var definitionsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WeaponDefsRef>());
            using var tuningQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WeaponTuning>());
            using var factionsQuery = em.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>());
            if (!definitionsQuery.IsEmptyIgnoreFilter || !tuningQuery.IsEmptyIgnoreFilter)
                throw new InvalidOperationException("Weapon content is already initialized in this world.");
            if (factionsQuery.CalculateEntityCount() > 1) throw new InvalidOperationException("FactionRelations must be a singleton.");
            // Resolve and validate config before taking ownership of native memory.
            var definition = catalog.Get<WeaponTuningDefinition>(new ContentId("config_weapons"));
            var report = new Game.Data.Validation.ValidationReport("Weapon boot");
            definition.Validate(report.For("content:config_weapons"), null);
            if (report.HasErrors) throw new Game.Data.Validation.ContentValidationException(report);
            var tuning = new WeaponTuning { MaxProjectiles = definition.MaxProjectiles,
                FixedConvergenceRad = math.radians(definition.FixedConvergenceDeg), ExplosionImpulsePerDamage = definition.ExplosionImpulsePerDamage,
                MaxTargetRange = definition.MaxTargetRange, UnderCursorRadius = definition.UnderCursorRadius, MaxCandidates = definition.MaxCandidates };
            BlobAssetReference<WeaponDefsBlob> blob = default;
            FactionRelations factions = default;
            Entity definitionsEntity = Entity.Null, tuningEntity = Entity.Null, factionsEntity = Entity.Null;
            int firstCreated = created.Count;
            try
            {
                blob = BuildBlob(catalog);
                if (factionsQuery.IsEmptyIgnoreFilter) factions = FactionRelations.CreateDefault(Allocator.Persistent);
                definitionsEntity = em.CreateSingleton(new WeaponDefsRef { Value = blob });
                tuningEntity = em.CreateSingleton(tuning);
                if (factions.Stance.IsCreated) factionsEntity = em.CreateSingleton(factions);
#if UNITY_EDITOR
                em.SetName(definitionsEntity, "Cfg.WeaponDefsRef"); em.SetName(tuningEntity, "Cfg.WeaponTuning");
                if (factionsEntity != Entity.Null) em.SetName(factionsEntity, "Cfg.FactionRelations");
#endif
                created.Add(definitionsEntity); created.Add(tuningEntity);
                if (factionsEntity != Entity.Null) created.Add(factionsEntity);
            }
            catch
            {
                if (factionsEntity != Entity.Null && em.Exists(factionsEntity)) em.DestroyEntity(factionsEntity);
                if (tuningEntity != Entity.Null && em.Exists(tuningEntity)) em.DestroyEntity(tuningEntity);
                if (definitionsEntity != Entity.Null && em.Exists(definitionsEntity)) em.DestroyEntity(definitionsEntity);
                if (created.Count > firstCreated) created.RemoveRange(firstCreated, created.Count - firstCreated);
                factions.Dispose(); if (blob.IsCreated) blob.Dispose(); throw;
            }
        }
        public static BlobAssetReference<WeaponDefsBlob> BuildBlob(ContentCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var definitions = catalog.All<WeaponDefinition>();
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<WeaponDefsBlob>();
            var weapons = builder.Allocate(ref root.Weapons, definitions.Count + 1);
            weapons[0] = default;
            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i]; UnityEngine.Color32 color = d.TracerColor;
                var w = new WeaponDef
                {
                    Index = (ushort)(i + 1), Type = d.DamageType, IsFixed = (byte)(d.TraverseHalfArcDeg == 0 ? 1 : 0),
                    DefaultFireGroups = (byte)d.DefaultFireGroups, ExplodeOnExpire = (byte)(d.ExplodeOnExpire ? 1 : 0),
                    TriggerIntervalTicks = Game.Core.SimTime.TicksPerSecond / d.FireRate, BurstIntervalTicks = d.BurstInterval * Game.Core.SimTime.TicksPerSecond,
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
        public static float ComputeSustainedDps(in WeaponDef w)
        {
            // Index zero is the all-zero "none" row, including its timing fields.
            if (!(w.TriggerIntervalTicks > 0) || !math.isfinite(w.TriggerIntervalTicks)) return 0;
            return (w.Delivery == WeaponDelivery.Beam ? w.Damage :
                (w.Damage + w.ExplosionDamage * .5f) * w.PelletsPerRound * w.BurstCount) * (SimTime.TicksPerSecond / w.TriggerIntervalTicks);
        }
    }
}
