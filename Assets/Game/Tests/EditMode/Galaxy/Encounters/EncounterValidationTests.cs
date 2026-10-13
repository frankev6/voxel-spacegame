using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content.Validators;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    /// <summary>ENC-002: one fail/pass fixture for each stated code; isolated in-memory content only.</summary>
    public sealed class EncounterValidationTests
    {
        FactionDefinition player, enemy;
        EncounterConfigDefinition config;
        ShipBlueprintDefinition blueprint;
        AiProfileDefinition civilian, combat;
        AiConfigDefinition ai;
        BlockDefinition missingBlock;
        ItemDefinition missingItem;

        [SetUp] public void SetUp()
        {
            player = Faction("faction_ark", true);
            enemy = Faction("faction_enemy"); Edit(enemy, "basePlayerRelation", -100);
            config = Config(); blueprint = TestContentFactory.Create<ShipBlueprintDefinition>("ship_test");
            civilian = TestContentFactory.Create<AiProfileDefinition>("ai_civilian", so => TestContentFactory.Set(so, "role", (int)AiRole.Civilian));
            combat = TestContentFactory.Create<AiProfileDefinition>("ai_combat");
            ai = TestContentFactory.Create<AiConfigDefinition>("config_ai", so => TestContentFactory.Set(so, "defaultProfile", civilian));
            missingBlock = TestContentFactory.Create<BlockDefinition>("block_missing");
            missingItem = TestContentFactory.Create<ItemDefinition>("item_missing");
        }
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        static FactionDefinition Faction(string id, bool player = false) => TestContentFactory.Create<FactionDefinition>(id, so =>
        { TestContentFactory.Set(so, "isPlayerFaction", player); TestContentFactory.Set(so, "relationLocked", player); });
        static EncounterConfigDefinition Config(string id = "config_encounters") => TestContentFactory.Create<EncounterConfigDefinition>(id);
        EncounterDefinition Encounter() => TestContentFactory.Create<EncounterDefinition>("encounter_test", so =>
        {
            TestContentFactory.Set(so, "faction", enemy);
            var rows = so.FindProperty("ships"); rows.arraySize = 1;
            ShipRow(rows.GetArrayElementAtIndex(0), blueprint, 1, 1, civilian);
        });
        static void ShipRow(SerializedProperty row, ShipBlueprintDefinition bp, int min, int max, AiProfileDefinition profile)
        {
            row.FindPropertyRelative("blueprint").objectReferenceValue = bp;
            row.FindPropertyRelative("countMin").intValue = min; row.FindPropertyRelative("countMax").intValue = max;
            row.FindPropertyRelative("aiProfile").objectReferenceValue = profile;
        }
        static void Edit(ContentDefinition definition, string field, object value) => TestContentFactory.Edit(definition, so => TestContentFactory.Set(so, field, value));
        static void Stance(FactionDefinition source, FactionDefinition target, FactionStanceValue value, int index = 0) => TestContentFactory.Edit(source, so =>
        {
            var rows = so.FindProperty("stances"); if (rows.arraySize <= index) rows.arraySize = index + 1;
            var row = rows.GetArrayElementAtIndex(index); row.FindPropertyRelative("other").objectReferenceValue = target;
            row.FindPropertyRelative("stance").intValue = (int)value;
        });
        void Doctrine(FactionDefinition faction, ShipBlueprintDefinition bp, float weight = 1, AiProfileDefinition profile = null) => TestContentFactory.Edit(faction, so =>
        {
            var rows = so.FindProperty("doctrine"); rows.arraySize = 1;
            var row = rows.GetArrayElementAtIndex(0); row.FindPropertyRelative("blueprint").objectReferenceValue = bp;
            row.FindPropertyRelative("weight").floatValue = weight; row.FindPropertyRelative("aiProfile").objectReferenceValue = profile;
        });
        static ValidationReport Local(ContentDefinition definition) => TestContentFactory.Validate(definition);
        ContentCatalog Catalog(ValidationReport report, params ContentDefinition[] definitions)
        {
            var list = new List<ContentDefinition> { missingBlock, missingItem, blueprint, civilian, combat, ai };
            list.AddRange(definitions);
            var catalog = ContentCatalog.Build(list, report); Clean(report); return catalog;
        }
        ValidationReport Global(params ContentDefinition[] definitions)
        {
            var report = new ValidationReport("ENC fixtures");
            var catalog = Catalog(report, definitions); new EncounterContentValidator().Validate(catalog, report); return report;
        }
        ValidationReport WithFallback(EncounterDefinition encounter)
        {
            var report = new ValidationReport("ENC fallback");
            var catalog = Catalog(report, encounter, enemy);
            encounter.Validate(report.For("test:" + encounter.IdString), new ContentValidationContext("test:" + encounter.IdString, true, _ => true, catalog));
            return report;
        }
        static void Clean(ValidationReport report) => Assert.AreEqual(0, report.Entries.Count, report.ToText());
        static void Only(ValidationReport report, string code, ValidationSeverity severity = ValidationSeverity.Error)
        {
            Assert.AreEqual(1, report.Entries.Count, report.ToText()); Assert.AreEqual(code, report.Entries[0].Code);
            Assert.AreEqual(severity, report.Entries[0].Severity); Assert.IsNotEmpty(report.Entries[0].AssetPath);
        }

        [Test] public void E001_Fails()
        {
            Only(Global(config), "ENC-E001");
            Only(Global(config, player, Faction("faction_second_player", true)), "ENC-E001");
            Edit(player, "relationLocked", false); Only(Local(player), "ENC-E001");
        }
        [Test] public void E001_Passes() { Clean(Local(player)); Clean(Global(config, player, enemy)); }
        [Test] public void E002_Fails()
        {
            var list = new List<ContentDefinition> { player, config };
            for (int i = 0; i < 64; i++) list.Add(Faction("faction_count_" + i));
            Only(Global(list.ToArray()), "ENC-E002");
        }
        [Test] public void E002_Passes()
        {
            var list = new List<ContentDefinition> { player, config };
            for (int i = 0; i < 63; i++) list.Add(Faction("faction_count_" + i));
            Clean(Global(list.ToArray()));
        }
        [Test] public void E003_Fails()
        {
            foreach (var target in new[] { null, enemy, player }) { Stance(enemy, target, FactionStanceValue.Hostile); Only(Local(enemy), "ENC-E003"); }
            var other = Faction("faction_other");
            Stance(enemy, other, (FactionStanceValue)255); Only(Local(enemy), "ENC-E003");
            Stance(enemy, other, FactionStanceValue.Friendly); Stance(enemy, other, FactionStanceValue.Hostile, 1); Only(Local(enemy), "ENC-E003");
            TestContentFactory.Edit(enemy, so => so.FindProperty("stances").arraySize = 1);
            Stance(other, enemy, FactionStanceValue.Hostile); Only(Global(config, player, enemy, other), "ENC-E003");
            Stance(player, other, FactionStanceValue.Friendly); Only(Local(player), "ENC-E003");
        }
        [Test] public void E003_Passes()
        {
            var other = Faction("faction_other");
            Stance(enemy, other, FactionStanceValue.Hostile); Stance(enemy, other, FactionStanceValue.Hostile, 1);
            Stance(other, enemy, FactionStanceValue.Hostile); Clean(Local(enemy)); Clean(Local(other)); Clean(Global(config, player, enemy, other));
        }
        [Test] public void E004_Fails()
        {
            Doctrine(enemy, null); Only(Local(enemy), "ENC-E004");
            foreach (float weight in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            { Doctrine(enemy, blueprint, weight); Only(Local(enemy), "ENC-E004"); }
        }
        [Test] public void E004_Passes() { Clean(Local(enemy)); Doctrine(enemy, blueprint, .1f); Clean(Local(enemy)); }
        static void Own(FactionDefinition faction, AuthoredStarSystemDefinition system) => TestContentFactory.Edit(faction, so =>
        { var rows = so.FindProperty("ownedStarSystems"); rows.arraySize = 1; rows.GetArrayElementAtIndex(0).objectReferenceValue = system; });
        [Test] public void E005_Fails()
        {
            var other = Faction("faction_other"); var system = TestContentFactory.Create<AuthoredStarSystemDefinition>("starsys_test");
            Own(enemy, system); Own(other, system); Only(Global(config, player, enemy, other, system), "ENC-E005");
        }
        [Test] public void E005_Passes()
        {
            var system = TestContentFactory.Create<AuthoredStarSystemDefinition>("starsys_test"); Own(enemy, system);
            TestContentFactory.Edit(enemy, so => { var rows = so.FindProperty("ownedStarSystems"); rows.arraySize = 2; rows.GetArrayElementAtIndex(1).objectReferenceValue = system; });
            Clean(Global(config, player, enemy, system));
        }
        [Test] public void E006_Fails()
        {
            foreach (string token in new[] { "", "blue", "--Color-faction" }) { Edit(enemy, "mapColorToken", token); Only(Local(enemy), "ENC-E006"); }
        }
        [Test] public void E006_Passes() { Edit(enemy, "mapColorToken", "--color-faction-neutral"); Clean(Local(enemy)); }

        [Test] public void E010_Fails() { var e = Encounter(); Edit(e, "faction", null); Only(Local(e), "ENC-E010"); }
        [Test] public void E010_Passes() => Clean(Local(Encounter()));
        [Test] public void E011_Fails()
        {
            var e = Encounter(); TestContentFactory.Edit(e, so => so.FindProperty("ships").arraySize = 0); Only(Local(e), "ENC-E011");
            e = Encounter(); Edit(e, "ships.Array.data[0].blueprint", null); Only(Local(e), "ENC-E011");
            e = Encounter(); Edit(e, "useFactionDoctrine", true); Only(Local(e), "ENC-E011");
        }
        [Test] public void E011_Passes()
        {
            var e = Encounter(); Clean(Local(e)); Doctrine(enemy, blueprint);
            Edit(e, "useFactionDoctrine", true); TestContentFactory.Edit(e, so => so.FindProperty("ships").arraySize = 0); Clean(Local(e));
        }
        [Test] public void E012_Fails()
        {
            foreach (var failure in new (string Field, int Value)[] { ("ships.Array.data[0].countMin", 0), ("ships.Array.data[0].countMax", 0),
                ("ships.Array.data[0].countMax", 11), ("doctrineMinShips", 0), ("doctrineMaxShips", 11) })
            { var e = Encounter(); Edit(e, failure.Field, failure.Value); Only(Local(e), "ENC-E012"); }
            var total = Encounter();
            TestContentFactory.Edit(total, so => { var rows = so.FindProperty("ships"); rows.arraySize = 2; for (int i = 0; i < 2; i++) ShipRow(rows.GetArrayElementAtIndex(i), blueprint, 1, 6, civilian); });
            Only(Local(total), "ENC-E012");
        }
        [Test] public void E012_Passes()
        {
            var e = Encounter(); Edit(e, "ships.Array.data[0].countMax", 10); Clean(Local(e));
            Doctrine(enemy, blueprint); Edit(e, "useFactionDoctrine", true); Edit(e, "doctrineMinShips", 1); Edit(e, "doctrineMaxShips", 10); Clean(Local(e));
        }
        [Test] public void E013_Fails()
        {
            foreach (var failure in new (string Field, object Value)[] { ("minDanger", -1), ("maxDanger", 4), ("minDanger", 4),
                ("directorWeight", -1f), ("directorWeight", float.NaN), ("directorWeight", float.PositiveInfinity) })
            { var e = Encounter(); Edit(e, failure.Field, failure.Value); Only(Local(e), "ENC-E013"); }
        }
        [Test] public void E013_Passes() { var e = Encounter(); Edit(e, "directorWeight", 1f); Clean(Local(e)); }
        [Test] public void E014_Fails()
        {
            foreach (var failure in new (string Field, float Value)[] { ("spawnDistanceMinM", 99f), ("spawnDistanceMinM", 2600f),
                ("spawnDistanceMaxM", 10001f), ("spawnDistanceMinM", float.NaN), ("spawnDistanceMaxM", float.PositiveInfinity), ("shipSpacingM", -1f) })
            { var e = Encounter(); Edit(e, failure.Field, failure.Value); Only(Local(e), "ENC-E014"); }
        }
        [Test] public void E014_Passes() { var e = Encounter(); Edit(e, "spawnDistanceMinM", 100f); Edit(e, "spawnDistanceMaxM", 10000f); Clean(Local(e)); }
        [Test] public void E015_Fails()
        {
            var e = Encounter(); Edit(e, "kind", (int)EncounterKind.Merchant); Edit(e, "ships.Array.data[0].aiProfile", combat); Only(Local(e), "ENC-E015");
            Edit(e, "ships.Array.data[0].aiProfile", null); Only(Local(e), "ENC-E015");
            Doctrine(enemy, blueprint, 1, combat); Edit(e, "useFactionDoctrine", true); Only(Local(e), "ENC-E015");
        }
        [Test] public void E015_Passes()
        {
            var e = Encounter(); Edit(e, "kind", (int)EncounterKind.Merchant); Clean(Local(e));
            Edit(e, "ships.Array.data[0].aiProfile", null); Edit(enemy, "defaultAiProfile", civilian); Clean(Local(e));
            Edit(enemy, "defaultAiProfile", null); Clean(WithFallback(e));
            Doctrine(enemy, blueprint, 1, civilian); Edit(e, "useFactionDoctrine", true); Clean(Local(e));
        }
        [Test] public void E020_Fails()
        {
            Only(Global(player), "ENC-E020"); Only(Global(player, Config("config_wrong")), "ENC-E020"); Only(Global(player, config, Config("config_extra")), "ENC-E020");
            foreach (string field in new[] { "firstRollDelaySeconds", "rollIntervalSeconds", "minPlayerDistanceM", "combatMinDistanceM", "arrivalGraceSeconds",
                "arrivalGraceDistanceM", "placementStepDegrees", "bodyClearanceM", "despawnDistanceM", "despawnDelaySeconds", "hailDistanceM" })
                foreach (float value in new[] { 0f, float.NaN, float.PositiveInfinity }) { var c = Config(); Edit(c, field, value); Only(Local(c), "ENC-E020"); }
            foreach (string field in new[] { "maxActiveDirectorInstances", "placementTries", "maxInstances", "maxShipsAlive", "provokePenalty", "provokeIntervalTicks" })
            { var c = Config(); Edit(c, field, 0); Only(Local(c), "ENC-E020"); }
            var cap = Config(); Edit(cap, "maxShipsAlive", 61); Only(Local(cap), "ENC-E020");
            var thresholds = Config(); Edit(thresholds, "hostileAtOrBelow", 50); Only(Local(thresholds), "ENC-E020");
            var chances = Config(); TestContentFactory.Edit(chances, so => so.FindProperty("dangerSpawnChance").arraySize = 3); Only(Local(chances), "ENC-E020");
            foreach (float value in new[] { -1f, 1.1f, float.NaN }) { var c = Config(); Edit(c, "dangerSpawnChance.Array.data[1]", value); Only(Local(c), "ENC-E020"); }
        }
        [Test] public void E020_Passes() { Clean(Local(config)); Clean(Global(config, player, enemy)); Edit(config, "maxShipsAlive", 60); Clean(Local(config)); }
        [Test] public void W001_Fails()
        {
            var e = Encounter(); Edit(enemy, "basePlayerRelation", 0); Edit(e, "directorWeight", 1f);
            Only(Global(config, player, enemy, e), "ENC-W001", ValidationSeverity.Warning);
        }
        [Test] public void W001_Passes()
        {
            var e = Encounter(); Edit(enemy, "basePlayerRelation", -25); Edit(e, "directorWeight", 1f); Clean(Global(config, player, enemy, e));
        }
        [Test] public void W002_Fails()
        {
            var e = Encounter(); Edit(enemy, "basePlayerRelation", 0); Edit(e, "kind", (int)EncounterKind.Story);
            Only(Global(config, player, enemy, e), "ENC-W002", ValidationSeverity.Warning);
        }
        [Test] public void W002_Passes() { var e = Encounter(); Edit(e, "kind", (int)EncounterKind.Story); Clean(Global(config, player, enemy, e)); }
    }
}
