using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation.Ai;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>The two AI-013 checks: shipped formation/catalog/blob parity and 20-metre slot validation (SPEC-23).</summary>
    public sealed class FormationTests
    {
        NativeLeakDetectionMode previousMode;
        [SetUp] public void SetUp()
        {
            previousMode = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
        }
        [TearDown] public void TearDown() { DestroyAll(); NativeLeakDetection.Mode = previousMode; }

        [Test] public void Bake()
        {
            var definitions = new List<FormationDefinition>();
            foreach (string name in new[] { "formation_wedge", "formation_line", "formation_column" })
            {
                var definition = AssetDatabase.LoadAssetAtPath<FormationDefinition>("Assets/Game/Content/Ai/" + name + ".asset");
                Assert.NotNull(definition, name); Assert.AreEqual(name, definition.IdString);
                Assert.AreEqual(10, definition.Slots.Count);
                Assert.IsFalse(Validate(definition).HasErrors);
                for (int i = 0; i < 10; i++)
                {
                    float distance = name == "formation_column" ? (i + 1) * 40 : (i / 2 + 1) * 40;
                    var expected = name == "formation_column" ? new Vector2(0, -distance) :
                        new Vector2(i % 2 == 0 ? -distance : distance, name == "formation_wedge" ? -distance : 0);
                    Assert.AreEqual(expected, definition.Slots[i], name + " slot " + i);
                }
                definitions.Add(definition);
            }
            var profile = Create<AiProfileDefinition>("ai_test");
            var configDefinition = Create<AiConfigDefinition>("config_ai", so => Set(so, "defaultProfile", profile));
            var catalog = Catalog(profile, configDefinition, definitions.ToArray());
            Assert.AreEqual(1, catalog.IndexOf(definitions[2])); // column, line, wedge: ordinal, one-based family indices
            Assert.AreEqual(2, catalog.IndexOf(definitions[1]));
            Assert.AreEqual(3, catalog.IndexOf(definitions[0]));
            using var world = new World("Formation bake");
            var em = world.EntityManager;
            var created = new List<Entity>();
            BlobAssetReference<AiProfilesBlob> blob = default;
            try
            {
                AiContent.CreateSingletons(em, catalog, created);
                var config = em.GetComponentData<AiConfig>(created[0]);
                blob = config.Profiles;
                ref var rows = ref config.Profiles.Value.Formations;
                Assert.AreEqual(4, rows.Length);
                Assert.AreEqual(0ul, rows[0].IdHash.Value); Assert.AreEqual(0, rows[0].Slots.Length);
                foreach (var definition in definitions)
                {
                    ref var row = ref rows[catalog.IndexOf(definition)];
                    Assert.AreEqual(definition.Id.Hash, row.IdHash); Assert.AreEqual(definition.Slots.Count, row.Slots.Length);
                    for (int i = 0; i < row.Slots.Length; i++)
                        Assert.AreEqual(new float2(definition.Slots[i].x, definition.Slots[i].y), row.Slots[i]);
                }
                Assert.AreEqual(2, blob.Value.Profiles.Length, "Formation rows share the existing profile blob/lifetime.");
                Assert.AreEqual(profile.Id.Hash, blob.Value.Profiles[0].IdHash);
                AiContent.CreateSingletons(em, catalog, created);
                Assert.AreEqual(1, created.Count, "Boot creation remains idempotent.");
                Assert.AreEqual(blob, em.GetComponentData<AiConfig>(created[0]).Profiles);
            }
            finally
            {
                em.CompleteAllTrackedJobs();
                if (created.Count > 0 && em.Exists(created[0])) em.DestroyEntity(created[0]);
                if (blob.IsCreated) blob.Dispose();
            }
        }

        [Test] public void Validation_Spacing()
        {
            var formation = Create<FormationDefinition>("formation_test");
            AssertCode(formation, "slots");
            Slots(formation, new Vector2(0, 0)); Assert.IsFalse(Validate(formation).HasErrors);
            foreach (var pair in new[]
            {
                new[] { Vector2.zero, Vector2.zero },
                new[] { Vector2.zero, new Vector2(19.99f, 0) },
                new[] { Vector2.zero, new Vector2(12, 15.99f) }
            })
            {
                Slots(formation, pair); AssertCode(formation, "slots[1]");
            }
            foreach (var pair in new[]
            {
                new[] { Vector2.zero, new Vector2(20, 0) },
                new[] { Vector2.zero, new Vector2(12, 16) },
                new[] { new Vector2(-40, -40), new Vector2(40, -40) }
            })
            {
                Slots(formation, pair); Assert.IsFalse(Validate(formation).HasErrors, "Exactly 20 metres is valid.");
            }
            Slots(formation, Vector2.zero, new Vector2(50, 0), new Vector2(51, 0)); AssertCode(formation, "slots[2]");
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Slots(formation, new Vector2(invalid, 0)); AssertCode(formation, "slots[0]");
                Slots(formation, new Vector2(0, invalid)); AssertCode(formation, "slots[0]");
            }

            // Invalid data must fail before allocating a boot singleton or a persistent blob.
            var profile = Create<AiProfileDefinition>("ai_test");
            var config = Create<AiConfigDefinition>("config_ai", so => Set(so, "defaultProfile", profile));
            Slots(formation, Vector2.zero, new Vector2(19, 0));
            using var world = new World("Invalid formation bake");
            var created = new List<Entity>();
            var exception = Assert.Throws<ContentValidationException>(() => AiContent.CreateSingletons(world.EntityManager, Catalog(profile, config, formation), created));
            StringAssert.Contains("AI-E004", exception.Message);
            Assert.AreEqual(0, created.Count);
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<AiConfig>());
            Assert.AreEqual(0, query.CalculateEntityCount());
            // No authored formations is legal during M2: reserve only entry zero, disposed through Profiles.
            BlobAssetReference<AiProfilesBlob> blob = default;
            try
            {
                AiContent.CreateSingletons(world.EntityManager, Catalog(profile, config), created);
                var baked = world.EntityManager.GetComponentData<AiConfig>(created[0]); blob = baked.Profiles;
                Assert.AreEqual(1, baked.Profiles.Value.Formations.Length); Assert.AreEqual(0, baked.Profiles.Value.Formations[0].Slots.Length);
            }
            finally
            {
                world.EntityManager.CompleteAllTrackedJobs();
                if (created.Count > 0 && world.EntityManager.Exists(created[0])) world.EntityManager.DestroyEntity(created[0]);
                if (blob.IsCreated) blob.Dispose();
            }
        }

        static ContentCatalog Catalog(AiProfileDefinition profile, AiConfigDefinition config, params FormationDefinition[] formations)
        {
            var definitions = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing"), profile, config };
            definitions.AddRange(formations);
            var report = new ValidationReport("Formations");
            var catalog = ContentCatalog.Build(definitions, report); Assert.IsFalse(report.HasErrors, report.ToText()); return catalog;
        }
        static void Slots(FormationDefinition definition, params Vector2[] slots) => Edit(definition, so =>
        {
            var property = so.FindProperty("slots"); property.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++) property.GetArrayElementAtIndex(i).vector2Value = slots[i];
        });
        static void AssertCode(FormationDefinition definition, string field)
        {
            var report = Validate(definition);
            Assert.AreEqual(1, report.ErrorCount, report.ToText());
            Assert.AreEqual("AI-E004", report.Entries[0].Code); Assert.AreEqual(field, report.Entries[0].Field);
        }
    }
}
