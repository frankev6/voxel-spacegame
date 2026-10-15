using System;
using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Ai
{
    /// <summary>Implements SPEC-23 §4.3: AiContent.</summary>
    public static class AiContent
    {
        /// <summary>Creates the boot-owned profiles/formations blob. The caller retains AiConfig.Profiles and disposes it after completing world jobs.</summary>
        public static void CreateSingletons(EntityManager em, ContentCatalog catalog, List<Entity> created)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (created == null) throw new ArgumentNullException(nameof(created));
            var configs = catalog.All<AiConfigDefinition>();
            var definitions = catalog.All<AiProfileDefinition>();
            var formations = catalog.All<FormationDefinition>();
            var report = new ValidationReport("AiContent");
            if (configs.Count != 1)
            {
                report.Error("AI-E002", "ContentDatabase", nameof(AiConfigDefinition), "Exactly one AI config definition is required.");
                throw new ContentValidationException(report);
            }
            var config = configs[0];
            config.Validate(report.For("content:" + config.IdString), null);
            ushort defaultIndex = catalog.IndexOf(config.DefaultProfile);
            if (config.DefaultProfile != null && defaultIndex == 0)
                report.Error("AI-E002", "content:" + config.IdString, "defaultProfile", "The default AI profile must be in the content catalog.");
            foreach (var definition in definitions)
            {
                definition.Validate(report.For("content:" + definition.IdString), null);
                for (int i = 0; i < definition.AbilityRules.Count; i++)
                {
                    var ability = definition.AbilityRules[i].ability;
                    if (ability != null && catalog.IndexOf(ability) == 0)
                        report.Error("AI-E003", "content:" + definition.IdString,
                            $"abilityRules[{i}].ability", "The ability rule must reference an ability in the content catalog.");
                }
            }
            foreach (var formation in formations)
                formation.Validate(report.For("content:" + formation.IdString), null);
            if (report.HasErrors) throw new ContentValidationException(report);
            using var existing = em.CreateEntityQuery(ComponentType.ReadOnly<AiConfig>());
            if (!existing.IsEmpty) return;

            BlobAssetReference<AiProfilesBlob> blob = default;
            Entity entity = Entity.Null;
            try
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<AiProfilesBlob>();
                var profiles = builder.Allocate(ref root.Profiles, definitions.Count + 1);
                for (int i = 0; i < profiles.Length; i++)
                {
                    var d = i == 0 ? config.DefaultProfile : definitions[i - 1];
                    ref var p = ref profiles[i];
                    p.Role = d.Role; p.Facing = d.Facing; p.AvoidFriendlyFire = (byte)(d.AvoidFriendlyFire ? 1 : 0);
                    p.Reserved = 0;
                    p.ThinkIntervalTicks = d.ThinkIntervalTicks;
                    p.SensorRange = d.SensorRange; p.PreferredRangeFraction = d.PreferredRangeFraction;
                    p.MinEngageRange = d.MinEngageRange; p.MaxEngageRange = d.MaxEngageRange; p.Throttle = d.Throttle; p.FleeHullFraction = d.FleeHullFraction;
                    p.LeashRadius = d.LeashRadius; p.PatrolRadius = d.PatrolRadius;
                    p.PlayerBias = d.PlayerBias; p.Stickiness = d.Stickiness;
                    p.OrbitStepRad = d.OrbitStepRad; p.OrbitFlipThinks = d.OrbitFlipThinks;
                    p.IdHash = d.Id.Hash;
                    // Allocate entry 0 independently too: copying a populated BlobArray header invalidates its offsets.
                    var rules = builder.Allocate(ref p.AbilityRules, d.AbilityRules.Count);
                    for (int j = 0; j < rules.Length; j++)
                    {
                        var rule = d.AbilityRules[j];
                        rules[j] = new AiAbilityRuleDef
                        {
                            AbilityIndex = catalog.IndexOf(rule.ability),
                            Trigger = rule.trigger,
                            Threshold = rule.threshold,
                        };
                    }
                }
                var formationRows = builder.Allocate(ref root.Formations, formations.Count + 1);
                formationRows[0] = default;
                builder.Allocate(ref formationRows[0].Slots, 0);
                for (int i = 0; i < formations.Count; i++)
                {
                    var definition = formations[i];
                    ref var target = ref formationRows[i + 1];
                    target.IdHash = definition.Id.Hash;
                    var slots = builder.Allocate(ref target.Slots, definition.Slots.Count);
                    for (int j = 0; j < slots.Length; j++)
                        slots[j] = new float2(definition.Slots[j].x, definition.Slots[j].y);
                }
                blob = builder.CreateBlobAssetReference<AiProfilesBlob>(Allocator.Persistent);
                entity = em.CreateSingleton(new AiConfig
                {
                    Profiles = blob, HashCellSize = config.HashCellSize, CombatBubbleRadius = config.CombatBubbleRadius,
                    ArrivalRadius = config.ArrivalRadius, AvoidanceMargin = config.AvoidanceMargin,
                    DormantThinkMultiplier = config.DormantThinkMultiplier, DefaultProfileIndex = defaultIndex,
                });
                created.Add(entity);
            }
            catch
            {
                if (entity != Entity.Null && em.Exists(entity)) em.DestroyEntity(entity);
                if (blob.IsCreated) blob.Dispose();
                throw;
            }
        }
    }
}
