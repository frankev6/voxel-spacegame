using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration;
using Game.Editor.Migration.Converters;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>SPEC-72 MIG-014: real export stubs and partial-migration safety in a disposable root.</summary>
    public sealed class BodyTemplateConverterTests
    {
        const string Root = "Assets/Game/Content/_BodyTemplateMigrationTest";
        LegacyContentIndex index;
        MigrationManifest manifest;
        bool ownsRoot;

        [SetUp] public void SetUp()
        {
            Assert.IsFalse(AssetDatabase.IsValidFolder(Root) || Directory.Exists(Root), "Never remove existing user content.");
            ownsRoot = true;
            manifest = new MigrationManifest();
            var report = new ValidationReport("MIG");
            index = LegacyContentIndex.Load(Path.GetFullPath("Import/Legacy"), report);
            Assert.IsFalse(report.HasErrors, report.ToText());
        }

        [TearDown] public void Clean()
        {
            if (ownsRoot) AssetDatabase.DeleteAsset(Root);
            ownsRoot = false;
        }

        LegacyMigrationContext Convert(bool force = false)
        {
            var report = new ValidationReport("MIG");
            var writer = new LegacyAssetWriter(Root, manifest, force, report);
            var context = new LegacyMigrationContext(index, LegacyIdMap.Default, writer, report, force, Path.GetFullPath("Import/Legacy"));
            new BodyTemplateConverter().Convert(context);
            Assert.IsFalse(report.HasErrors, report.ToText());
            return context;
        }

        [Test] public void RealExport_TwentyStubs()
        {
            var first = Convert();
            Assert.AreEqual(20, first.Writer.Outcomes.Count);
            CollectionAssert.AreEquivalent(Expected.Select(r => r.Id), first.Writer.Outcomes.Select(o => Path.GetFileNameWithoutExtension(o.AssetPath)));
            foreach (var row in Expected)
            {
                var body = Load(row.Id);
                Assert.NotNull(body, row.Id);
                Assert.AreEqual(row.Id, body.IdString);
                Assert.AreEqual(row.Name, body.DisplayName, row.Id);
                Assert.AreEqual(row.Role, body.Role, row.Id);
                Assert.AreEqual(row.Tag, body.StoryTag, row.Id);
                Assert.AreEqual("", body.Description, row.Id); // The export has only blank/placeholder body descriptions.
                Assert.IsNull(body.Visual, row.Id);
                Assert.IsNull(body.Blueprint, row.Id);
                Assert.AreEqual(100f, body.RadiusMinM, row.Id); // Never import legacy radii/spawn data.
                Assert.AreEqual(100f, body.RadiusMaxM, row.Id);
                Assert.IsTrue(manifest.TryGetHash(PathFor(row.Id), out ulong hash));
                Assert.AreEqual(StableHash.Hash64(File.ReadAllBytes(PathFor(row.Id))), hash, row.Id);
            }
            Assert.AreEqual(20, first.Report.Entries.Count(e => e.Code == "MIG-I091"));
            Assert.AreEqual(20, first.Report.Entries.Count(e => e.Code == "MIG-I090" && e.Field == "color"));
            Assert.IsFalse(first.Report.Entries.Any(e => e.Code == "MIG-W090"), first.Report.ToText());
            Assert.AreEqual(20, first.Report.Entries.Where(e => e.Code == "MIG-I091").Select(e => e.AssetPath).Distinct().Count());
            Assert.AreEqual("Bodies", new BodyTemplateConverter().Name);
            Assert.AreEqual(420, new BodyTemplateConverter().Order);
            var bytes = first.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => File.ReadAllBytes(o.AssetPath));
            var guids = first.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => AssetDatabase.AssetPathToGUID(o.AssetPath));
            var second = Convert();
            Assert.AreEqual(20, second.Writer.Outcomes.Count);
            foreach (var outcome in second.Writer.Outcomes)
            {
                Assert.AreEqual(WriteOutcome.Unchanged, outcome.Outcome, outcome.AssetPath);
                CollectionAssert.AreEqual(bytes[outcome.AssetPath], File.ReadAllBytes(outcome.AssetPath), outcome.AssetPath);
                Assert.AreEqual(guids[outcome.AssetPath], AssetDatabase.AssetPathToGUID(outcome.AssetPath), outcome.AssetPath);
            }

            // Simulate later owned world authoring: the manifest includes it, so this stub pass must preserve it.
            const string id = "body_star_blackhole";
            string path = PathFor(id);
            var authored = Load(id);
            authored.name = "Authored black-hole object";
            EditorUtility.SetDirty(authored);
            using (var so = new SerializedObject(authored))
            {
                so.FindProperty("radiusMinM").floatValue = 321f;
                so.FindProperty("radiusMaxM").floatValue = 456f;
                so.FindProperty("interactionDistanceM").floatValue = 987f;
                so.FindProperty("hasCollider").boolValue = false;
                so.FindProperty("shape.wallThicknessCells").intValue = 7;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(authored);
            byte[] worldBytes = File.ReadAllBytes(path);
            ulong ownedHash = StableHash.Hash64(worldBytes);
            manifest.SetHash(path, ownedHash);
            Assert.AreEqual(WriteOutcome.Unchanged, Convert().Writer.Outcomes.Single(o => o.AssetPath == path).Outcome);
            CollectionAssert.AreEqual(worldBytes, File.ReadAllBytes(path));

            using (var so = new SerializedObject(authored))
            {
                so.FindProperty("displayName").stringValue = "Hand-authored black hole";
                so.FindProperty("t2LoadDistanceM").floatValue = 12345f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssetIfDirty(authored);
            byte[] handBytes = File.ReadAllBytes(path);
            var protectedRun = Convert();
            Assert.AreEqual(WriteOutcome.SkippedHandEdited, protectedRun.Writer.Outcomes.Single(o => o.AssetPath == path).Outcome);
            CollectionAssert.AreEqual(handBytes, File.ReadAllBytes(path));
            Assert.IsTrue(manifest.TryGetHash(path, out ulong unchangedHash));
            Assert.AreEqual(ownedHash, unchangedHash, "A protected asset must not be adopted into the manifest.");
            Assert.AreEqual(1, protectedRun.Report.Entries.Count(e => e.Code == "MIG-I011"));
            Assert.AreEqual(WriteOutcome.Updated, Convert(true).Writer.Outcomes.Single(o => o.AssetPath == path).Outcome);
            Assert.AreEqual("Black Hole", authored.DisplayName);
            Assert.AreEqual("Authored black-hole object", authored.name, "A partial refresh preserves Unity object metadata too.");
            Assert.AreEqual(321f, authored.RadiusMinM);
            Assert.AreEqual(456f, authored.RadiusMaxM);
            Assert.AreEqual(987f, authored.InteractionDistanceM);
            Assert.IsFalse(authored.HasCollider);
            Assert.AreEqual(7, authored.Shape.wallThicknessCells);
            Assert.AreEqual(12345f, authored.T2LoadDistanceM, "Force applies only to stub fields.");
            Assert.IsTrue(manifest.TryGetHash(path, out ulong forcedHash));
            Assert.AreEqual(StableHash.Hash64(File.ReadAllBytes(path)), forcedHash);
            Assert.AreEqual(guids[path], AssetDatabase.AssetPathToGUID(path));
        }

        static string PathFor(string id) => Root + "/Bodies/" + id + ".asset";
        static BodyTemplateDefinition Load(string id) => AssetDatabase.LoadAssetAtPath<BodyTemplateDefinition>(PathFor(id));

        static readonly (string Id, string Name, BodyRole Role, string Tag)[] Expected =
        {
            ("body_star", "Star", BodyRole.Star, "star"), ("body_star_blackhole", "Black Hole", BodyRole.Star, "black_hole"),
            ("body_planet_barren", "Barren Planet", BodyRole.Planet, "barren_planet"), ("body_planet_gas_giant", "Gas Giant", BodyRole.Planet, "gas_giant"),
            ("body_planet_ice", "Ice Planet", BodyRole.Planet, "ice_planet"), ("body_planet_ice_giant", "Ice Giant", BodyRole.Planet, "ice_giant"),
            ("body_planet_rocky", "Rocky Planet", BodyRole.Planet, "rocky_planet"), ("body_planet_terrestrial", "Terrestrial Planet", BodyRole.Planet, "terrestrial_planet"),
            ("body_planet_water", "Ocean Planet", BodyRole.Planet, "ocean_planet"), ("body_moon", "Moon", BodyRole.Moon, "moon"),
            ("body_station", "Station", BodyRole.Station, "station"), ("body_station_ark", "ARK Station", BodyRole.Station, "ark_station"),
            ("body_station_abandoned", "Abandoned Station", BodyRole.Station, "abandoned_station"), ("body_asteroid_belt", "Asteroid Belt", BodyRole.Belt, "asteroid_belt"),
            ("body_gate_active", "Active Gate", BodyRole.LaneGate, "gate_active"), ("body_gate_dormant", "Dormant Gate", BodyRole.LaneGate, "gate_dormant"),
            ("body_jump_point", "Jump Point", BodyRole.LaneGate, "jump_point"), ("body_derelict_ship", "Derelict Ship", BodyRole.PointOfInterest, "derelict"),
            ("body_debris_field", "Debris Field", BodyRole.PointOfInterest, "debris_field"), ("body_resource_cache", "Resource Cache", BodyRole.PointOfInterest, "resource_cache"),
        };
    }
}
