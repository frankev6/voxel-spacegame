using System;
using System.Collections.Generic;
using System.Linq;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Logic.Nodes;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Editor.Content;
using Game.Editor.Content.Validators;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    /// <summary>SPEC-32 TRV-002: defaults, one failing/passing case per diagnostic, and real shipped-content validation.</summary>
    public sealed class TravelConfigTests
    {
        sealed class Fixture
        {
            internal readonly TravelConfigDefinition Config;
            internal readonly BodyTemplateDefinition Gate;
            internal readonly StarSystemTemplateDefinition System;
            internal readonly ShipBlueprintDefinition Blueprint;
            internal readonly VoxelConfigDefinition Voxel;
            internal readonly GalaxyGenerationDefinition Galaxy;
            internal readonly List<ContentDefinition> Definitions = new();
            internal Fixture()
            {
                var fuel = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/resource_fuel.asset");
                var cue = AssetDatabase.LoadAssetAtPath<AudioCueDefinition>("Assets/Game/Content/Audio/cue_warp.asset");
                Assert.IsNotNull(fuel); Assert.IsNotNull(cue);
                Config = TestContentFactory.Create<TravelConfigDefinition>("config_travel", so =>
                { TestContentFactory.Set(so, "fuelItem", fuel); TestContentFactory.Set(so, "departCue", cue); TestContentFactory.Set(so, "arriveCue", cue); });
                Blueprint = TestContentFactory.Create<ShipBlueprintDefinition>("ship_travel_test", so =>
                { so.FindProperty("boundsMin").vector3IntValue = new Vector3Int(-10, -10, -10); so.FindProperty("boundsMax").vector3IntValue = new Vector3Int(9, 9, 9); });
                Gate = TestContentFactory.Create<BodyTemplateDefinition>("body_travel_gate", so =>
                {
                    TestContentFactory.Set(so, "role", (int)BodyRole.LaneGate); TestContentFactory.Set(so, "interaction", (int)InteractionKind.LaneGate);
                    TestContentFactory.Set(so, "destructible", false); TestContentFactory.Set(so, "interactPrompt", "Jump");
                    TestContentFactory.Set(so, "radiusMinM", 100f); TestContentFactory.Set(so, "radiusMaxM", 100f);
                    TestContentFactory.Set(so, "interactionDistanceM", 150f); TestContentFactory.Set(so, "structureSource", (int)StructureSource.Blueprint);
                    TestContentFactory.Set(so, "blueprint", Blueprint);
                });
                System = TestContentFactory.Create<StarSystemTemplateDefinition>("systpl_travel_test", so => TestContentFactory.Set(so, "laneRingMarginM", 10000f));
                Voxel = TestContentFactory.Create<VoxelConfigDefinition>("config_voxel");
                Galaxy = TestContentFactory.Create<GalaxyGenerationDefinition>("galaxy_travel_test");
                Definitions.AddRange(new ContentDefinition[] { Config, Gate, System, Blueprint, Voxel, Galaxy, fuel, cue,
                    AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset") });
            }
            internal ValidationReport Validate()
            {
                var report = new ValidationReport("TRV-002 fixture"); var catalog = ContentCatalog.Build(Definitions, report);
                foreach (var config in Definitions.OfType<TravelConfigDefinition>()) config.Validate(report.For("content:" + config.IdString), null);
                new TravelContentValidator().Validate(catalog, report); return report;
            }
            internal void Shape(Vector3Int size)
            {
                TestContentFactory.Edit(Gate, so =>
                { TestContentFactory.Set(so, "structureSource", (int)StructureSource.ProceduralShape); so.FindProperty("shape.sizeCells").vector3IntValue = size; });
            }
            internal void Lane(string source, ICondition root = null, bool failed = false)
            {
                ((List<AuthoredLane>)Galaxy.AuthoredLanes).Add(new AuthoredLane { condition = new CompiledCondition() });
                TestContentFactory.Edit(Galaxy, so => TestContentFactory.Set(so, "authoredLanes.Array.data[0].condition.source", source));
                Galaxy.AuthoredLanes[0].condition.SetCompiled(root, failed);
            }
        }
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        static void Set(ContentDefinition definition, string field, object value) => TestContentFactory.Edit(definition, so => TestContentFactory.Set(so, field, value));
        static void Expect(Fixture fixture, string code = null)
        {
            var report = fixture.Validate();
            CollectionAssert.AreEqual(code == null ? Array.Empty<string>() : new[] { code }, report.Entries.Select(entry => entry.Code), report.ToText());
            if (code != null) Assert.AreEqual(code == "TRV-W001" ? ValidationSeverity.Warning : ValidationSeverity.Error, report.Entries[0].Severity);
        }
        static void BadConfig(string code, params (string field, object value)[] edits)
        { foreach (var edit in edits) { var fixture = new Fixture(); Set(fixture.Config, edit.field, edit.value); Expect(fixture, code); } }
        [Test] public void Defaults()
        {
            var fixture = new Fixture(); Expect(fixture);
            var config = fixture.Config;
            Assert.AreEqual(30, config.SpoolTicks); Assert.AreEqual(30, config.ArrivalTicks); Assert.AreEqual(.5f, config.CoverMinSeconds);
            Assert.AreEqual(2, config.RevealDelayFrames); Assert.AreEqual(15f, config.TransitTimeoutSeconds); Assert.AreEqual(150f, config.ArrivalStandoffM);
            Assert.AreEqual(50f, config.ArrivalStepM); Assert.AreEqual(16, config.ArrivalMaxSteps); Assert.AreEqual(60f, config.PlayerClearanceM);
            Assert.AreEqual(.5f, config.FuelPerLy); Assert.AreEqual(1, config.FuelMinPerJump); Assert.AreEqual(0, config.AncientGateFuel);
            Assert.AreEqual(600, config.LaneRefreshTicks); Assert.AreEqual(10f, config.WarpFxScale); Assert.AreEqual(60, config.CruiseSpoolTicks);
            Assert.AreEqual(3000f, config.CruiseMinDistanceM); Assert.AreEqual(300f, config.CruiseStandoffM); Assert.AreEqual(300, config.CruiseDamageCooldownTicks); Assert.AreEqual(2000f, config.EscortRadiusM);
            var shipped = AssetDatabase.LoadAssetAtPath<TravelConfigDefinition>("Assets/Game/Content/Config/config_travel.asset"); Assert.IsNotNull(shipped);
            Assert.AreEqual("config_travel", shipped.IdString); Assert.AreEqual("resource_fuel", shipped.FuelItem.IdString);
            Assert.AreEqual("cue_warp", shipped.DepartCue.IdString); Assert.AreSame(shipped.DepartCue, shipped.ArriveCue);
            foreach (var property in typeof(TravelConfigDefinition).GetProperties().Where(property => property.DeclaringType == typeof(TravelConfigDefinition)))
                Assert.AreEqual(property.GetValue(config), property.GetValue(shipped), property.Name);
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(ContentDatabaseBuilder.DatabasePath); Assert.IsNotNull(database);
            Assert.Contains(shipped, database.Definitions.ToList(), "Rebuild the actual content database after importing config_travel.");
            var report = ContentValidatorRunner.Validate(database);
            Assert.IsEmpty(report.Entries.Where(entry => entry.Severity == ValidationSeverity.Error && entry.Code.StartsWith("TRV-", StringComparison.Ordinal)), report.ToText());
            Assert.IsTrue(UnityEditor.TypeCache.GetTypesDerivedFrom<IContentDatabaseValidator>().Contains(typeof(TravelContentValidator)), "The real runner discovers the validator.");
        }
        [Test] public void TRV_E001_Fails()
        {
            var absent = new Fixture(); absent.Definitions.Remove(absent.Config); Expect(absent, "TRV-E001");
            var duplicate = new Fixture(); var second = TestContentFactory.Create<TravelConfigDefinition>("config_travel_second", so =>
            { TestContentFactory.Set(so, "fuelItem", duplicate.Config.FuelItem); TestContentFactory.Set(so, "departCue", duplicate.Config.DepartCue); TestContentFactory.Set(so, "arriveCue", duplicate.Config.ArriveCue); });
            duplicate.Definitions.Add(second); Expect(duplicate, "TRV-E001");
            var wrong = new Fixture(); Set(wrong.Config, "id", "config_wrong"); Expect(wrong, "TRV-E001");
        }
        [Test] public void TRV_E001_Passes() => Expect(new Fixture());
        [Test] public void TRV_E002_Fails() => BadConfig("TRV-E002", ("spoolTicks", 0), ("spoolTicks", 601), ("arrivalTicks", 0), ("arrivalTicks", 601),
            ("coverMinSeconds", -.1f), ("coverMinSeconds", 5.1f), ("coverMinSeconds", float.NaN), ("transitTimeoutSeconds", 4.9f), ("transitTimeoutSeconds", float.PositiveInfinity),
            ("revealDelayFrames", -1), ("revealDelayFrames", 11), ("laneRefreshTicks", 59));
        [Test] public void TRV_E002_Passes()
        {
            var f = new Fixture(); Set(f.Config, "spoolTicks", 1); Set(f.Config, "arrivalTicks", 600); Set(f.Config, "coverMinSeconds", 0f);
            Set(f.Config, "transitTimeoutSeconds", 5f); Set(f.Config, "revealDelayFrames", 0); Set(f.Config, "laneRefreshTicks", 60); Expect(f);
            Set(f.Config, "spoolTicks", 600); Set(f.Config, "arrivalTicks", 1); Set(f.Config, "coverMinSeconds", 5f); Set(f.Config, "revealDelayFrames", 10); Expect(f);
        }
        [Test] public void TRV_E003_Fails() => BadConfig("TRV-E003", ("fuelItem", null), ("fuelPerLy", -.1f), ("fuelPerLy", float.NaN),
            ("fuelPerLy", float.PositiveInfinity), ("fuelMinPerJump", -1), ("ancientGateFuel", -1));
        [Test] public void TRV_E003_Passes()
        { var f = new Fixture(); Set(f.Config, "fuelPerLy", 0f); Set(f.Config, "fuelMinPerJump", 0); Set(f.Config, "ancientGateFuel", 0); Expect(f); }
        [Test] public void TRV_E004_Fails() => BadConfig("TRV-E004", ("arrivalStandoffM", 49f), ("arrivalStandoffM", 2001f), ("arrivalStandoffM", float.NaN),
            ("arrivalStepM", 9f), ("arrivalStepM", 501f), ("arrivalStepM", float.PositiveInfinity), ("arrivalMaxSteps", -1), ("arrivalMaxSteps", 65),
            ("playerClearanceM", -.1f), ("playerClearanceM", 151f), ("playerClearanceM", float.NaN));
        [Test] public void TRV_E004_Passes()
        {
            var f = new Fixture(); Set(f.Config, "arrivalStandoffM", 50f); Set(f.Config, "playerClearanceM", 0f); Set(f.Config, "arrivalStepM", 10f); Set(f.Config, "arrivalMaxSteps", 0); Expect(f);
            Set(f.Config, "arrivalStandoffM", 2000f); Set(f.Config, "playerClearanceM", 2000f); Set(f.Config, "arrivalStepM", 500f); Set(f.Config, "arrivalMaxSteps", 64); Expect(f);
        }
        [Test] public void TRV_E005_Fails()
        {
            foreach (var edit in new (string field, object value)[] { ("interaction", (int)InteractionKind.None), ("destructible", true), ("interactionDistanceM", 49f), ("interactionDistanceM", float.NaN), ("interactPrompt", " ") })
            { var f = new Fixture(); Set(f.Gate, edit.field, edit.value); Expect(f, "TRV-E005"); }
        }
        [Test] public void TRV_E005_Passes() { var f = new Fixture(); Set(f.Gate, "interactionDistanceM", 50f); Expect(f); }
        [Test] public void TRV_E006_Fails()
        { var f = new Fixture(); Set(f.System, "laneRingMarginM", 310f); Expect(f, "TRV-E006"); Set(f.System, "laneRingMarginM", 309f); Expect(f, "TRV-E006"); }
        [Test] public void TRV_E006_Passes() { var f = new Fixture(); Set(f.System, "laneRingMarginM", 311f); Expect(f); }
        [Test] public void TRV_E007_Fails()
        {
            var blueprint = new Fixture(); TestContentFactory.Edit(blueprint.Blueprint, so => so.FindProperty("boundsMax").vector3IntValue = new Vector3Int(199, 0, 0));
            Expect(blueprint, "TRV-E007");
            // Negative bounds and inclusive max + 1 must both contribute to the enclosing radius.
            var negative = new Fixture(); TestContentFactory.Edit(negative.Blueprint, so => so.FindProperty("boundsMin").vector3IntValue = new Vector3Int(-300, 0, 0)); Expect(negative, "TRV-E007");
            var shape = new Fixture(); shape.Shape(new Vector3Int(200, 200, 200)); Expect(shape, "TRV-E007");
            var missingVoxel = new Fixture(); missingVoxel.Shape(new Vector3Int(2, 3, 6)); missingVoxel.Definitions.Remove(missingVoxel.Voxel); Expect(missingVoxel, "TRV-E007");
        }
        [Test] public void TRV_E007_Passes()
        {
            var f = new Fixture(); Set(f.Gate, "radiusMinM", 3.5f);
            TestContentFactory.Edit(f.Blueprint, so => { so.FindProperty("boundsMin").vector3IntValue = new Vector3Int(-2, -3, -6); so.FindProperty("boundsMax").vector3IntValue = new Vector3Int(1, 2, 5); }); Expect(f);
            f.Shape(new Vector3Int(2, 3, 6)); Expect(f); // 0.5 * length(2,3,6) == 3.5: equality is allowed for E007.
        }
        [Test] public void TRV_E008_Fails() => BadConfig("TRV-E008", ("departCue", null), ("arriveCue", null), ("warpFxScale", 0f), ("warpFxScale", float.PositiveInfinity),
            ("cruiseSpoolTicks", 0), ("cruiseMinDistanceM", 499f), ("cruiseMinDistanceM", float.NaN), ("cruiseStandoffM", 49f), ("escortRadiusM", -.1f));
        [Test] public void TRV_E008_Passes()
        { var f = new Fixture(); Set(f.Config, "warpFxScale", .1f); Set(f.Config, "cruiseSpoolTicks", 1); Set(f.Config, "cruiseMinDistanceM", 500f); Set(f.Config, "cruiseStandoffM", 50f); Set(f.Config, "escortRadiusM", 0f); Expect(f); }
        [Test] public void TRV_W001_Fails() { var f = new Fixture(); f.Lane(" "); Expect(f, "TRV-W001"); }
        [Test] public void TRV_W001_Passes()
        {
            var ungated = new Fixture(); ungated.Lane(""); Expect(ungated);
            var gated = new Fixture(); gated.Lane("false", new BoolLiteral(false)); Expect(gated);
            var invalid = new Fixture(); invalid.Lane("bad", null, true); Expect(invalid);
        }
    }
}
