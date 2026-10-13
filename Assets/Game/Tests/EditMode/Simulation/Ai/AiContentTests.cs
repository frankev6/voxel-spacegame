using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation.Ai;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEditor;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>AI-003: scalar blob/asset parity, default entry, lifetime and authoring codes (SPEC-23 §4.2–4.3).</summary>
    public sealed class AiContentTests
    {
        NativeLeakDetectionMode previousMode;
        [SetUp] public void SetUp()
        { previousMode = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace; }
        [TearDown] public void TearDown()
        { NativeLeakDetection.Mode = previousMode; DestroyAll(); }

        [Test]
        public void Blob_MatchesAssets()
        {
            var profiles = new List<AiProfileDefinition>();
            foreach (string id in new[] { "ai_turret", "ai_skittish", "ai_default", "ai_civilian" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<AiProfileDefinition>("Assets/Game/Content/Ai/" + id + ".asset");
                Assert.NotNull(asset, id); profiles.Add(asset);
                Assert.AreEqual(id, asset.IdString);
                Assert.AreEqual(id == "ai_turret" ? AiRole.Turret : id == "ai_civilian" ? AiRole.Civilian : AiRole.Combat, asset.Role);
                Assert.AreEqual(id == "ai_turret" ? 6 : 10, asset.ThinkIntervalTicks);
                Assert.AreEqual(id == "ai_turret" ? 1200f : 1500f, asset.SensorRange);
                Assert.AreEqual(.7f, asset.PreferredRangeFraction); Assert.AreEqual(80f, asset.MinEngageRange);
                Assert.AreEqual(AiFacing.FaceTarget, asset.Facing); Assert.AreEqual(id == "ai_skittish" ? 1f : .85f, asset.Throttle);
                Assert.AreEqual(id == "ai_skittish" ? .5f : id == "ai_civilian" ? 1f : .25f, asset.FleeHullFraction);
                Assert.AreEqual(3000f, asset.LeashRadius); Assert.AreEqual(600f, asset.PatrolRadius);
                Assert.AreEqual(id == "ai_skittish" ? 0f : .3f, asset.PlayerBias); Assert.AreEqual(.25f, asset.Stickiness);
                Assert.AreEqual(12, asset.OrbitFlipThinks); Assert.AreEqual(.35f, asset.OrbitStepRad);
                Assert.IsTrue(asset.AvoidFriendlyFire);
                Assert.AreEqual(id == "ai_default" ? 2 : 1, asset.AbilityRules.Count);
                for (int i = 0; i < asset.AbilityRules.Count; i++)
                {
                    bool shield = id == "ai_turret" || (id == "ai_default" && i == 0);
                    Assert.NotNull(asset.AbilityRules[i].ability);
                    Assert.AreEqual(shield ? "ability_shield_toggle" : "ability_speed_boost", asset.AbilityRules[i].ability.IdString);
                    Assert.AreEqual(shield ? AiAbilityTrigger.ShieldOff : AiAbilityTrigger.Fleeing, asset.AbilityRules[i].trigger);
                    Assert.AreEqual(0f, asset.AbilityRules[i].threshold);
                }
            }
            var definition = AssetDatabase.LoadAssetAtPath<AiConfigDefinition>("Assets/Game/Content/Config/config_ai.asset");
            Assert.NotNull(definition); Assert.AreEqual("config_ai", definition.IdString);
            Assert.NotNull(definition.DefaultProfile); Assert.AreEqual("ai_default", definition.DefaultProfile.IdString);
            Assert.AreEqual(128f, definition.HashCellSize); Assert.AreEqual(6, definition.DormantThinkMultiplier);
            Assert.AreEqual(2000f, definition.CombatBubbleRadius); Assert.AreEqual(40f, definition.ArrivalRadius);
            Assert.AreEqual(25f, definition.AvoidanceMargin);
            var defs = new List<ContentDefinition>(profiles); defs.Add(definition);
            var catalog = Catalog(defs.ToArray());
            using var world = new World("AiContent");
            var em = world.EntityManager;
            var created = new List<Entity>();
            BlobAssetReference<AiProfilesBlob> blob = default;
            try
            {
                AiContent.CreateSingletons(em, catalog, created);
                blob = em.GetComponentData<AiConfig>(created[0]).Profiles;
                AiContent.CreateSingletons(em, catalog, created);
                Assert.AreEqual(1, created.Count);
                using var query = em.CreateEntityQuery(ComponentType.ReadOnly<AiConfig>());
                Assert.AreEqual(1, query.CalculateEntityCount());
                var config = query.GetSingleton<AiConfig>();
                Assert.AreEqual(definition.HashCellSize, config.HashCellSize);
                Assert.AreEqual(definition.DormantThinkMultiplier, config.DormantThinkMultiplier);
                Assert.AreEqual(definition.CombatBubbleRadius, config.CombatBubbleRadius);
                Assert.AreEqual(definition.ArrivalRadius, config.ArrivalRadius); Assert.AreEqual(definition.AvoidanceMargin, config.AvoidanceMargin);
                Assert.AreEqual(2, config.DefaultProfileIndex, "ai_default follows ai_civilian in catalog order");
                Assert.AreEqual(catalog.IndexOf(definition.DefaultProfile), config.DefaultProfileIndex);
                Assert.AreEqual(profiles.Count + 1, blob.Value.Profiles.Length);
                foreach (var asset in profiles)
                    AssertProfile(asset, ref blob.Value.Profiles[catalog.IndexOf(asset)]);
                AssertProfile(definition.DefaultProfile, ref blob.Value.Profiles[0]);
                // AI-006 owns rule baking; the authored rules above remain validated content meanwhile.
            }
            finally
            {
                em.CompleteAllTrackedJobs();
                if (created.Count != 0 && em.Exists(created[0])) em.DestroyEntity(created[0]);
                if (blob.IsCreated) blob.Dispose();
            }
        }

        [Test]
        public void Validation_Codes()
        {
            var profile = Create<AiProfileDefinition>("ai_test");
            var ability = Create<AbilityDefinition>("ability_test");
            var config = Create<AiConfigDefinition>("config_ai", so => Set(so, "defaultProfile", profile));
            Assert.AreEqual(0, Validate(profile).ErrorCount); Assert.AreEqual(0, Validate(config).ErrorCount);
            (string Field, object Value)[] badProfile =
            {
                ("role", 255), ("facing", 255), ("thinkIntervalTicks", 0), ("thinkIntervalTicks", 121),
                ("sensorRange", 99f), ("preferredRangeFraction", .19f), ("preferredRangeFraction", 1.21f),
                ("minEngageRange", 9f), ("throttle", -.01f), ("throttle", 1.01f), ("fleeHullFraction", -.01f),
                ("fleeHullFraction", 1.01f), ("leashRadius", -1f), ("patrolRadius", -1f), ("playerBias", -.01f),
                ("playerBias", 2.01f), ("stickiness", -.01f), ("stickiness", 2.01f), ("orbitFlipThinks", -1),
                ("orbitFlipThinks", 61), ("orbitStepRad", -.01f), ("orbitStepRad", 1.51f),
                ("sensorRange", float.PositiveInfinity), ("preferredRangeFraction", float.NaN),
            };
            foreach (var trial in badProfile) InvalidScalar(profile, trial.Field, trial.Value, "AI-E001");
            (string Field, object Value)[] badConfig =
            {
                ("hashCellSize", 31f), ("dormantThinkMultiplier", 0), ("dormantThinkMultiplier", 21),
                ("combatBubbleRadius", -1f), ("arrivalRadius", .99f), ("avoidanceMargin", -1f),
                ("hashCellSize", float.PositiveInfinity), ("combatBubbleRadius", float.NaN),
            };
            foreach (var trial in badConfig) InvalidScalar(config, trial.Field, trial.Value, "AI-E002");
            Edit(config, so => Set(so, "defaultProfile", null)); AssertCode(config, "AI-E002", "defaultProfile");
            Edit(config, so => Set(so, "defaultProfile", profile));

            Rules(profile, ability, 8); Assert.AreEqual(0, Validate(profile).ErrorCount);
            Rules(profile, ability, 9); AssertCode(profile, "AI-E003", "abilityRules");
            Rules(profile, null, 1); AssertCode(profile, "AI-E003", "abilityRules[0].ability");
            Rules(profile, ability, 1);
            InvalidScalar(profile, "abilityRules.Array.data[0].trigger", 255, "AI-E003", "abilityRules[0].trigger");
            foreach (float value in new[] { -.01f, 1.01f, float.NaN, float.PositiveInfinity })
                InvalidScalar(profile, "abilityRules.Array.data[0].threshold", value, "AI-E003", "abilityRules[0].threshold");

            using var world = new World("InvalidAiContent");
            var created = new List<Entity>();
            var error = Assert.Throws<ContentValidationException>(() => AiContent.CreateSingletons(world.EntityManager, Catalog(profile), created));
            StringAssert.Contains("AI-E002", error.Message);
            var second = Create<AiConfigDefinition>("config_ai_other", so => Set(so, "defaultProfile", profile));
            error = Assert.Throws<ContentValidationException>(() => AiContent.CreateSingletons(world.EntityManager, Catalog(profile, config, second), created));
            StringAssert.Contains("AI-E002", error.Message);
            var foreign = Create<AiProfileDefinition>("ai_foreign");
            Edit(config, so => Set(so, "defaultProfile", foreign));
            error = Assert.Throws<ContentValidationException>(() => AiContent.CreateSingletons(world.EntityManager, Catalog(profile, config), created));
            StringAssert.Contains("AI-E002", error.Message);
            Assert.AreEqual(0, created.Count);
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AiConfig>());
            Assert.AreEqual(0, query.CalculateEntityCount(), "invalid content allocates no persistent profiles/singleton");
        }

        static void AssertProfile(AiProfileDefinition expected, ref AiProfile actual)
        {
            Assert.AreEqual(expected.Role, actual.Role); Assert.AreEqual(expected.Facing, actual.Facing);
            Assert.AreEqual(expected.AvoidFriendlyFire ? 1 : 0, actual.AvoidFriendlyFire);
            Assert.AreEqual(expected.ThinkIntervalTicks, actual.ThinkIntervalTicks);
            Assert.AreEqual(expected.SensorRange, actual.SensorRange); Assert.AreEqual(expected.PreferredRangeFraction, actual.PreferredRangeFraction);
            Assert.AreEqual(expected.MinEngageRange, actual.MinEngageRange); Assert.AreEqual(expected.Throttle, actual.Throttle);
            Assert.AreEqual(expected.FleeHullFraction, actual.FleeHullFraction); Assert.AreEqual(expected.LeashRadius, actual.LeashRadius);
            Assert.AreEqual(expected.PatrolRadius, actual.PatrolRadius); Assert.AreEqual(expected.PlayerBias, actual.PlayerBias);
            Assert.AreEqual(expected.Stickiness, actual.Stickiness); Assert.AreEqual(expected.OrbitFlipThinks, actual.OrbitFlipThinks);
            Assert.AreEqual(expected.OrbitStepRad, actual.OrbitStepRad, "the authored value is already radians");
            Assert.AreEqual(expected.Id.Hash, actual.IdHash); Assert.AreEqual(0, actual.Reserved);
        }
        static ContentCatalog Catalog(params ContentDefinition[] definitions)
        {
            var all = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing") };
            all.AddRange(definitions);
            var report = new ValidationReport("AiContentTests");
            var catalog = ContentCatalog.Build(all, report); Assert.IsFalse(report.HasErrors, report.ToText()); return catalog;
        }
        static void Rules(AiProfileDefinition profile, AbilityDefinition ability, int count)
            => Edit(profile, so =>
            {
                var rules = so.FindProperty("abilityRules"); rules.arraySize = count;
                for (int i = 0; i < count; i++)
                {
                    var rule = rules.GetArrayElementAtIndex(i);
                    rule.FindPropertyRelative("ability").objectReferenceValue = ability;
                    rule.FindPropertyRelative("trigger").intValue = (int)AiAbilityTrigger.Fleeing;
                    rule.FindPropertyRelative("threshold").floatValue = 0;
                }
            });
        static void AssertCode(ContentDefinition definition, string code, string field)
        {
            var report = Validate(definition);
            Assert.AreEqual(1, report.ErrorCount, report.ToText());
            Assert.AreEqual(code, report.Entries[0].Code); Assert.AreEqual(field, report.Entries[0].Field);
        }
        static void InvalidScalar(ContentDefinition definition, string field, object value, string code, string reportedField = null)
        {
            object original;
            using (var so = new SerializedObject(definition))
            {
                var property = so.FindProperty(field);
                original = property.propertyType == SerializedPropertyType.Float ? (object)property.floatValue : property.intValue;
            }
            Edit(definition, so => Set(so, field, value));
            try { AssertCode(definition, code, reportedField ?? field); }
            finally { Edit(definition, so => Set(so, field, original)); }
        }
    }
}
