using System.IO;
using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Editor.Migration;
using Game.Editor.Migration.Encounters;
using NUnit.Framework;
using UnityEditor;

namespace Game.Tests.EditMode.Editor.Migration
{
    public sealed class FleetEncounterConverterTests
    {
        [Test] public void RealExport_FourEncounters()
        {
            using var fixture = new EncounterContentTestFixture(); fixture.Factions();
            var rawProbe = FleetEncounterConverter.FirstFormation(fixture.HandOff.Fleets.Single(f => f.LegacyEntityId == "npc_probe_tutorial"));
            Assert.AreEqual("ship_frigate", rawProbe.Single().Id); Assert.AreEqual(3, rawProbe.Single().Count);
            foreach (string id in new[] { "npc_enemy_1", "npc_story_hunter" })
            { var raw = FleetEncounterConverter.FirstFormation(fixture.HandOff.Fleets.Single(f => f.LegacyEntityId == id)); Assert.AreEqual("ship_shuttle", raw.Single().Id); Assert.AreEqual(1, raw.Single().Count); }
            Assert.IsEmpty(FleetEncounterConverter.FirstFormation(fixture.HandOff.Fleets.Single(f => f.LegacyEntityId == "npc_traveling_merchant")));
            var first = fixture.Fleets();
            Assert.AreEqual(4, first.Writer.Outcomes.Count); Assert.AreEqual(4, first.Writer.Outcomes.Select(o => o.AssetPath).Distinct().Count());
            Assert.IsTrue(first.Writer.Outcomes.All(o => o.Outcome == WriteOutcome.Created));
            Assert.AreEqual(4, first.Report.Entries.Count(e => e.Code == "ENC-I001"));
            Assert.AreEqual(3, first.Report.Entries.Count(e => e.Code == "ENC-I002"));
            Check("encounter_raiders", "faction_raiders", EncounterKind.Ambush, "ship_fighter", 2, 3);
            Check("encounter_xeno_probe_tutorial", "faction_xeno", EncounterKind.Story, "ship_frigate", 3, 3);
            Check("encounter_whisper_hunter", "faction_raiders", EncounterKind.Story, "ship_red_elite", 1, 1);
            Check("encounter_traveling_merchant", "faction_vestari", EncounterKind.Merchant, "ship_freighter", 1, 1);
            var raiders = EncounterContentTestFixture.Encounter("encounter_raiders");
            Assert.AreEqual(1, raiders.DirectorWeight); Assert.AreEqual(1, raiders.MinDanger); Assert.AreEqual(3, raiders.MaxDanger);
            Assert.AreEqual(1600, raiders.SpawnDistanceMinM); Assert.AreEqual(2200, raiders.SpawnDistanceMaxM);
            var merchant = EncounterContentTestFixture.Encounter("encounter_traveling_merchant");
            Assert.AreEqual(.5f, merchant.DirectorWeight); Assert.AreEqual(0, merchant.MinDanger); Assert.AreEqual(2, merchant.MaxDanger);
            Assert.IsTrue(merchant.Hailable); Assert.IsTrue(merchant.UniquePerVisit); Assert.AreEqual("ai_civilian", merchant.Ships[0].aiProfile.IdString);
            Assert.AreEqual(AiRole.Civilian, merchant.Ships[0].aiProfile.Role);
            Assert.AreEqual("Encounters", new FleetEncounterConverter().Name); Assert.AreEqual(600, new FleetEncounterConverter().Order);
            var bytes = first.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => File.ReadAllBytes(o.AssetPath));
            var guids = first.Writer.Outcomes.ToDictionary(o => o.AssetPath, o => AssetDatabase.AssetPathToGUID(o.AssetPath));
            foreach (var outcome in fixture.Fleets().Writer.Outcomes)
            { Assert.AreEqual(WriteOutcome.Unchanged, outcome.Outcome); CollectionAssert.AreEqual(bytes[outcome.AssetPath], File.ReadAllBytes(outcome.AssetPath)); Assert.AreEqual(guids[outcome.AssetPath], AssetDatabase.AssetPathToGUID(outcome.AssetPath)); }
            string path = AssetDatabase.GetAssetPath(raiders);
            using (var so = new SerializedObject(raiders)) { so.FindProperty("shipSpacingM").floatValue = 42; so.ApplyModifiedPropertiesWithoutUndo(); }
            AssetDatabase.SaveAssetIfDirty(raiders); byte[] edited = File.ReadAllBytes(path);
            Assert.AreEqual(WriteOutcome.SkippedHandEdited, fixture.Fleets().Writer.Outcomes.Single(o => o.AssetPath == path).Outcome);
            CollectionAssert.AreEqual(edited, File.ReadAllBytes(path));
            fixture.Fleets(true); Assert.AreEqual(42, raiders.ShipSpacingM); Assert.AreEqual(guids[path], AssetDatabase.AssetPathToGUID(path));

            void Check(string id, string faction, EncounterKind kind, string ship, int min, int max)
            {
                var encounter = EncounterContentTestFixture.Encounter(id); Assert.IsNotNull(encounter);
                Assert.AreEqual(faction, encounter.Faction.IdString); Assert.AreEqual(kind, encounter.Kind); Assert.IsFalse(encounter.UseFactionDoctrine);
                Assert.AreEqual(ship, encounter.Ships.Single().blueprint.IdString); Assert.AreEqual(min, encounter.Ships[0].countMin); Assert.AreEqual(max, encounter.Ships[0].countMax);
                Assert.IsFalse(encounter.Condition.IsStale); Assert.IsFalse(encounter.Condition.HasErrors); Assert.IsTrue(encounter.Condition.IsAlwaysTrue);
            }
        }
    }
}
