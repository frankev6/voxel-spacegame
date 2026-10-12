using System;
using System.Linq;
using System.Reflection;
using Game.App.Boot;
using Game.Data;
using Game.Data.Combat;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Tests.Common;
using Game.Voxel.Damage;
using NUnit.Framework;
using UnityEditor;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.Voxel.Damage
{
    /// <summary>DMG-003: shipped values, extensible table, boot ownership and required content validation.</summary>
    public sealed class DamageConfigTests
    {
        const string ConfigPath = "Assets/Game/Content/Config/config_damage.asset";
        ContentDatabase database;
        DamageConfigDefinition config;
        [SetUp] public void SetUp()
        {
            database = ContentDatabaseBuilder.Rebuild();
            config = AssetDatabase.LoadAssetAtPath<DamageConfigDefinition>(ConfigPath);
            Assert.IsNotNull(config);
        }
        [Test] public void FromDefinition_MatchesDefault()
        {
            Assert.AreEqual(DamageConfig.Default, DamageConfig.FromDefinition(config));
            using var content = new ContentService(database);
            using var world = EcsTestWorld.Create();
            world.World.CreateSystem<DamageStreamsSystem>();
            content.CreateSingletons(world.EntityManager);
            using (BootSingletons.CreateAll(world.EntityManager, content.Catalog, content.PropMeshes))
            {
                using var query = world.EntityManager.CreateEntityQuery(typeof(DamageConfig), typeof(BootSingleton));
                Assert.AreEqual(1, query.CalculateEntityCount(), "boot must replace the owner's fallback, not duplicate it");
                var actual = query.GetSingleton<DamageConfig>();
                float[] expected = { 1, .6f, .8f, 1.5f, 1, .8f, .6f, 1.25f, 1, .7f, .7f, 2, 1, .5f, .75f, 2 };
                float[] mins = { 6, 3, 4, 3 }, maxs = { 6, 3, 18, 3 }, shields = { .8f, 1.25f, 1, 1 };
                Assert.AreEqual(4, actual.Table.Value.ArmorClassCount);
                Assert.AreEqual(4, actual.Table.Value.Types.Length);
                for (int t = 0; t < 4; t++)
                {
                    for (byte a = 0; a < 4; a++) Assert.AreEqual(expected[t * 4 + a], actual.Multiplier((DamageType)t, a));
                    var row = actual.Table.Value.Types[t];
                    Assert.AreEqual(mins[t], row.KickMin); Assert.AreEqual(maxs[t], row.KickMax);
                    Assert.AreEqual(shields[t], row.ShieldMultiplier); Assert.AreEqual(t == 3 ? 1 : 0, row.BypassShields);
                }
            }
            Assert.IsFalse(world.GetSingleton<DamageConfig>().Table.IsCreated, "shutdown must leave no dangling table in the surviving owner config");

            var fifth = Object.Instantiate(content.Catalog.All<DamageTypeDefinition>()[0]);
            var cue = ScriptableObject.CreateInstance<AudioCueDefinition>();
            var authored = Object.Instantiate(config);
            try
            {
                using (var so = new SerializedObject(fifth))
                {
                    so.FindProperty("id").stringValue = "dmg_test_fifth";
                    so.FindProperty("type").intValue = 4; // Test-only future enum value; no algorithm switch or production enum edit.
                    so.FindProperty("armorMultipliers").GetArrayElementAtIndex(3).floatValue = 7.25f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var report = new ValidationReport("fifth damage type");
                var defs = database.Definitions.Concat(new[] { fifth }).ToArray();
                var catalog = ContentCatalog.Build(defs, report);
                fifth.Validate(report.For("test:fifth"), new ContentValidationContext("test:fifth", false, defs.Contains, catalog));
                Assert.IsFalse(report.HasErrors, report.ToText());
                var table = DamageContent.BuildTable(catalog);
                try
                {
                    var extended = DamageConfig.Default; extended.Table = table;
                    Assert.AreEqual(5, table.Value.Types.Length);
                    Assert.AreEqual(7.25f, extended.Multiplier((DamageType)4, 3));
                }
                finally { table.Dispose(); }
                using (var so = new SerializedObject(cue)) { so.FindProperty("id").stringValue = "cue_test_damage"; so.ApplyModifiedPropertiesWithoutUndo(); }
                using (var so = new SerializedObject(authored))
                {
                    foreach (string field in new[] { "ramImpactCue", "cellBreakCue", "splitCue", "coreLostCue" }) so.FindProperty(field).objectReferenceValue = cue;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                var cues = DamageConfig.FromDefinition(authored);
                Assert.AreEqual(cue.Id.Hash, cues.CueRamImpact); Assert.AreEqual(cue.Id.Hash, cues.CueCellBreak);
                Assert.AreEqual(cue.Id.Hash, cues.CueSplit); Assert.AreEqual(cue.Id.Hash, cues.CueCoreLost);
            }
            finally { Object.DestroyImmediate(fifth); Object.DestroyImmediate(cue); Object.DestroyImmediate(authored); }
        }
        [Test] public void Validate_RejectsBadBudgets()
        {
            foreach (var field in typeof(DamageConfigDefinition).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                if (field.FieldType == typeof(int)) Bad(config, so => so.FindProperty(field.Name).intValue = 0, "DMG-E005", field.Name);
                if (field.FieldType == typeof(float)) Bad(config, so => so.FindProperty(field.Name).floatValue = -1, "DMG-E005", field.Name);
            }
            Bad(config, so => so.FindProperty("particleMaxCells").intValue = config.ChunkMaxCells, "DMG-E005", "particleMaxCells");
            var type = database.Definitions.OfType<DamageTypeDefinition>().Single(d => d.Type == DamageType.Kinetic);
            Bad(type, so => so.FindProperty("armorMultipliers").arraySize = 3, "DMG-E001", "armorMultipliers");
            Bad(type, so => so.FindProperty("armorMultipliers").GetArrayElementAtIndex(0).floatValue = -1, "DMG-E001", "armorMultipliers");
            Bad(type, so => so.FindProperty("shieldMultiplier").floatValue = -1, "DMG-E001", "shieldMultiplier");
            Bad(type, so => so.FindProperty("type").intValue = (int)DamageType.Energy, "DMG-E002", "type");
            var incomplete = database.Definitions.Where(d => !(d is DamageTypeDefinition t) || t.Type != DamageType.Ram).ToArray();
            var report = new ValidationReport("missing declared damage type");
            var catalog = ContentCatalog.Build(incomplete, report);
            type.Validate(report.For("test:missing-type"), new ContentValidationContext("test:missing-type", false, incomplete.Contains, catalog));
            Assert.IsTrue(report.Entries.Any(e => e.Code == "DMG-E002" && e.Field == "type"), report.ToText());
            Assert.Throws<ArgumentException>(() => DamageContent.BuildTable(catalog));
            var armor = database.Definitions.OfType<ArmorClassDefinition>().Single(a => a.Index == 0);
            Bad(armor, so => so.FindProperty("index").intValue = 1, "DMG-E004", "index");
            Bad(armor, so => so.FindProperty("index").intValue = 4, "DMG-E004", "index");
        }
        [Test] public void Validate_RejectsInvertedLifetimes()
        {
            Bad(config, so => so.FindProperty("chunkLifetimeMinSeconds").floatValue = config.ChunkLifetimeMaxSeconds + 1, "DMG-E006", "chunkLifetimeMinSeconds");
            Bad(config, so => so.FindProperty("chunkTumbleMin").floatValue = config.ChunkTumbleMax + 1, "DMG-E006", "chunkTumbleMin");
            var type = database.Definitions.OfType<DamageTypeDefinition>().First();
            Bad(type, so => so.FindProperty("kickMin").floatValue = type.KickMax + 1, "DMG-E003", "kickMin");
            Bad(type, so => so.FindProperty("kickMax").floatValue = -1, "DMG-E003", "kickMax");
        }
        void Bad(ContentDefinition source, Action<SerializedObject> change, string code, string field)
        {
            var clone = Object.Instantiate(source);
            try
            {
                using (var so = new SerializedObject(clone)) { change(so); so.ApplyModifiedPropertiesWithoutUndo(); }
                var definitions = database.Definitions.Select(d => d == source ? clone : d).ToArray();
                var report = new ValidationReport("DMG-003");
                var catalog = ContentCatalog.Build(definitions, report);
                const string path = "test:damage-definition";
                clone.Validate(report.For(path), new ContentValidationContext(path, true, definitions.Contains, catalog));
                Assert.IsTrue(report.Entries.Any(e => e.Severity == ValidationSeverity.Error && e.Code == code && e.Field == field && e.AssetPath == path), report.ToText());
            }
            finally { Object.DestroyImmediate(clone); }
        }
    }
}
