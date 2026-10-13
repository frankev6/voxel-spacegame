using System;
using Game.App.Boot;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Galaxy.Bodies;
using Game.Tests.Common;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>SPEC-31 SYS-002: exact defaults, factory/boot publication, and config/belt identity validation.</summary>
    public sealed class StreamingConfigTests
    {
        const string Path = "Assets/Game/Content/Config/config_streaming.asset";
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();

        [Test] public void Defaults()
        {
            var expected = new StreamingConfig
            {
                T2UnloadFactor = 1.25f, BubbleRadiusM = 900f, BubbleUnloadFactor = 1.25f,
                MaxGridSpawnsPerFrame = 2, MaxAsteroidJobsInFlight = 4, MaxAsteroidsPerSector = 8,
                MaxRenderDistanceM = 25000f, InteractionQueryRadiusM = 600f, PersistDamageMaxCells = 65536,
            };
            var definition = TestContentFactory.Create<StreamingConfigDefinition>("config_streaming");
            Assert.AreEqual(expected, StreamingConfigFactory.Create(definition));
            Assert.AreEqual(0, TestContentFactory.Validate(definition).ErrorCount);
            Assert.Throws<ArgumentNullException>(() => StreamingConfigFactory.Create(null));
            var asset = AssetDatabase.LoadAssetAtPath<StreamingConfigDefinition>(Path);
            Assert.NotNull(asset, "Lead must generate/catalog-register config_streaming before running the acceptance suite.");
            Assert.AreEqual("config_streaming", asset.IdString);
            Assert.AreEqual(expected, StreamingConfigFactory.Create(asset));
            Assert.AreEqual(0, TestContentFactory.Validate(asset).ErrorCount);

            using var content = new ContentService(ContentDatabaseBuilder.Rebuild());
            using var world = EcsTestWorld.Create();
            content.CreateSingletons(world.EntityManager);
            using var boot = BootSingletons.CreateAll(world.EntityManager, content.Catalog, content.PropMeshes);
            Assert.AreEqual(expected, world.GetSingleton<StreamingConfig>());
            using var query = world.EntityManager.CreateEntityQuery(typeof(StreamingConfig), typeof(BootSingleton));
            Assert.AreEqual(1, query.CalculateEntityCount());

            // Getters/factory must copy tuned values, not return hard-coded defaults.
            TestContentFactory.Edit(definition, so =>
            {
                TestContentFactory.Set(so, "t2UnloadFactor", 1.5f);
                TestContentFactory.Set(so, "bubbleRadiusM", 1000f);
                TestContentFactory.Set(so, "bubbleUnloadFactor", 1.75f);
                TestContentFactory.Set(so, "maxGridSpawnsPerFrame", 3);
                TestContentFactory.Set(so, "maxAsteroidJobsInFlight", 5);
                TestContentFactory.Set(so, "maxAsteroidsPerSector", 7);
                TestContentFactory.Set(so, "maxRenderDistanceM", 30000f);
                TestContentFactory.Set(so, "interactionQueryRadiusM", 750f);
                TestContentFactory.Set(so, "persistDamageMaxCells", 32768);
            });
            Assert.AreEqual(new StreamingConfig
            {
                T2UnloadFactor = 1.5f, BubbleRadiusM = 1000f, BubbleUnloadFactor = 1.75f,
                MaxGridSpawnsPerFrame = 3, MaxAsteroidJobsInFlight = 5, MaxAsteroidsPerSector = 7,
                MaxRenderDistanceM = 30000f, InteractionQueryRadiusM = 750f, PersistDamageMaxCells = 32768,
            }, StreamingConfigFactory.Create(definition));
            Assert.AreEqual(0, TestContentFactory.Validate(definition).ErrorCount);
        }

        [Test] public void Validation()
        {
            foreach (string field in new[] { "t2UnloadFactor", "bubbleUnloadFactor" })
            {
                Invalid(field, 1.049f, "SYS-E001");
                Invalid(field, float.NaN, "SYS-E001");
                Invalid(field, float.PositiveInfinity, "SYS-E001");
                Valid(field, 1.05f);
            }
            Invalid("bubbleRadiusM", 199f, "SYS-E002");
            Invalid("bubbleRadiusM", float.NaN, "SYS-E002");
            Invalid("bubbleRadiusM", float.PositiveInfinity, "SYS-E002");
            Valid("bubbleRadiusM", 200f);
            foreach (string field in new[] { "maxRenderDistanceM", "interactionQueryRadiusM" })
            {
                Invalid(field, 0f, "SYS-E004");
                Invalid(field, float.NaN, "SYS-E004");
                Invalid(field, float.NegativeInfinity, "SYS-E004");
            }
            foreach (string field in new[] { "maxGridSpawnsPerFrame", "maxAsteroidJobsInFlight", "maxAsteroidsPerSector", "persistDamageMaxCells" })
            {
                Invalid(field, 0, "SYS-E004");
                Valid(field, 1);
            }
            Invalid("maxAsteroidsPerSector", 9, "SYS-E004");
            Invalid("persistDamageMaxCells", 65537, "SYS-E004");

            var belt = TestContentFactory.Create<BodyTemplateDefinition>("body_test_belt", so =>
            {
                TestContentFactory.Set(so, "role", (int)BodyRole.Belt);
                TestContentFactory.Set(so, "beltWidthM", 1000f);
                TestContentFactory.Set(so, "sectorSizeM", 500f);
            });
            var procedural = TestContentFactory.Create<StarSystemTemplateDefinition>("systpl_test", so =>
            {
                TestContentFactory.Set(so, "maxPlanets", 0);
                TestContentFactory.Set(so, "maxBelts", 1);
                var pool = so.FindProperty("beltPool"); pool.arraySize = 1;
                var entry = pool.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("template").objectReferenceValue = belt;
                entry.FindPropertyRelative("weight").floatValue = 1f;
            });
            var authored = TestContentFactory.Create<AuthoredStarSystemDefinition>("starsys_test", so =>
            {
                var bodies = so.FindProperty("bodies"); bodies.arraySize = 1;
                var body = bodies.GetArrayElementAtIndex(0);
                body.FindPropertyRelative("key").stringValue = "belt";
                body.FindPropertyRelative("role").intValue = (int)BodyRole.Belt;
                body.FindPropertyRelative("template").objectReferenceValue = belt;
                body.FindPropertyRelative("orbitRadiusM").floatValue = 3000f;
            });
            var config = TestContentFactory.Create<StreamingConfigDefinition>("config_streaming");
            var catalogReport = new ValidationReport("catalog");
            var catalog = ContentCatalog.Build(new ContentDefinition[] { config, belt, procedural, authored,
                TestContentFactory.Create<BlockDefinition>("block_missing"), TestContentFactory.Create<ItemDefinition>("item_missing") }, catalogReport);
            Assert.AreEqual(0, catalogReport.ErrorCount, catalogReport.ToText());
            AssertBeltErrors(config, catalog, 0);
            // Two rings reserve sectors 0..4095 and 4096..8191; a third ring's first subslot is 65536.
            TestContentFactory.Edit(belt, so => TestContentFactory.Set(so, "beltWidthM", 1001f));
            AssertBeltErrors(config, catalog, 2);
            TestContentFactory.Edit(procedural, so => TestContentFactory.Set(so, "firstOrbitRadiusM", 330000f));
            TestContentFactory.Edit(authored, so => so.FindProperty("bodies").GetArrayElementAtIndex(0).FindPropertyRelative("orbitRadiusM").floatValue = 330000f);
            TestContentFactory.Edit(belt, so => TestContentFactory.Set(so, "beltWidthM", 1000f));
            AssertBeltErrors(config, catalog, 2); // More than 4096 angles aliases the next ring even if ushort arithmetic fits.
            TestContentFactory.Edit(belt, so => TestContentFactory.Set(so, "beltWidthM", 500f));
            AssertBeltErrors(config, catalog, 0); // One ring can use the remaining ushort sector range without collisions.
            TestContentFactory.Edit(procedural, so => TestContentFactory.Set(so, "firstOrbitRadiusM", 700000f));
            TestContentFactory.Edit(authored, so => so.FindProperty("bodies").GetArrayElementAtIndex(0).FindPropertyRelative("orbitRadiusM").floatValue = 700000f);
            AssertBeltErrors(config, catalog, 2); // Even one ring cannot encode more than 8192 sectors.
            TestContentFactory.Edit(procedural, so => TestContentFactory.Set(so, "firstOrbitRadiusM", 3000f));
            TestContentFactory.Edit(authored, so => so.FindProperty("bodies").GetArrayElementAtIndex(0).FindPropertyRelative("orbitRadiusM").floatValue = 3000f);
            TestContentFactory.Edit(procedural, so => TestContentFactory.Set(so, "maxPlanets", 233));
            AssertBeltErrors(config, catalog, 1); // Procedural capacity uses its maximum possible orbit, not just FirstOrbitRadiusM.
            TestContentFactory.Edit(procedural, so => TestContentFactory.Set(so, "maxPlanets", 0));
            TestContentFactory.Edit(belt, so => TestContentFactory.Set(so, "sectorSizeM", 0f));
            AssertBeltErrors(config, catalog, 2);
        }

        static void AssertBeltErrors(StreamingConfigDefinition config, ContentCatalog catalog, int count)
        {
            var report = new ValidationReport("streaming");
            config.Validate(report.For(Path), new ContentValidationContext(Path, true, _ => true, catalog));
            Assert.AreEqual(count, report.ErrorCount, report.ToText());
            foreach (var entry in report.Entries)
            {
                Assert.AreEqual("SYS-E003", entry.Code);
                Assert.AreEqual(Path, entry.AssetPath);
                StringAssert.Contains("body_test_belt", entry.Message);
                Assert.IsTrue(entry.Field == "systpl_test.beltPool[0]" || entry.Field == "starsys_test.bodies[0]", entry.Field);
            }
        }

        static void Invalid(string field, object value, string code)
        {
            var definition = TestContentFactory.Create<StreamingConfigDefinition>("config_test", so => TestContentFactory.Set(so, field, value));
            var report = TestContentFactory.Validate(definition);
            Assert.AreEqual(1, report.ErrorCount, report.ToText());
            Assert.AreEqual(code, report.Entries[0].Code);
            Assert.AreEqual(field, report.Entries[0].Field);
        }

        static void Valid(string field, object value)
        {
            var definition = TestContentFactory.Create<StreamingConfigDefinition>("config_test", so => TestContentFactory.Set(so, field, value));
            var report = TestContentFactory.Validate(definition);
            Assert.AreEqual(0, report.ErrorCount, report.ToText());
        }
    }
}
