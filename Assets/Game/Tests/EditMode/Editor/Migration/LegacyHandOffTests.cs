using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Data.Validation;
using Game.Editor.Migration;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Migration
{
    /// <summary>MIG-015: the three specified hand-off checks against the real export (SPEC-72 §4.7).</summary>
    public sealed class LegacyHandOffTests
    {
        static LegacyHandOff Build(out ValidationReport report)
        {
            report = new ValidationReport("MIG");
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Import", "Legacy"));
            var index = LegacyContentIndex.Load(root, report);
            var handOff = LegacyHandOff.Build(index, LegacyIdMap.Default, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            return handOff;
        }

        [Test]
        public void Fleets_FormationsResolved()
        {
            var handOff = Build(out _);
            Assert.AreEqual(4, handOff.Fleets.Count);
            var probe = handOff.Fleets.Single(f => f.LegacyEntityId == "npc_probe_tutorial");
            Assert.AreEqual("encounter_xeno_probe_tutorial", probe.SuggestedId);
            Assert.AreEqual(1, probe.Formations.Count);
            CollectionAssert.AreEqual(new[] { "ship_frigate", "ship_frigate", "ship_frigate" }, probe.Formations[0]);
            var enemy = handOff.Fleets.Single(f => f.LegacyEntityId == "npc_enemy_1");
            Assert.AreEqual("encounter_raiders", enemy.SuggestedId);
            Assert.AreEqual(3, enemy.Formations.Count);
            CollectionAssert.AreEqual(new[] { "ship_shuttle" }, enemy.Formations[0]);
            CollectionAssert.AreEqual(new[] { "ship_fighter" }, enemy.Formations[1]);
            CollectionAssert.AreEqual(new[] { "ship_shuttle", "ship_frigate" }, enemy.Formations[2]);
            var hunter = handOff.Fleets.Single(f => f.LegacyEntityId == "npc_story_hunter");
            Assert.AreEqual("encounter_whisper_hunter", hunter.SuggestedId);
            Assert.AreEqual(3, hunter.Formations.Count);
            for (int i = 0; i < enemy.Formations.Count; i++) CollectionAssert.AreEqual(enemy.Formations[i], hunter.Formations[i]);
            var merchant = handOff.Fleets.Single(f => f.LegacyEntityId == "npc_traveling_merchant");
            Assert.AreEqual("encounter_traveling_merchant", merchant.SuggestedId);
            Assert.AreEqual("event_merchant_shady", merchant.LegacyNarrativeEncounterId);
            Assert.IsEmpty(merchant.Formations);
            Assert.AreEqual("Neutral", merchant.Allegiance);
            Assert.IsTrue(merchant.AutoInteractOnContact);
            Assert.IsTrue(enemy.UseFactionDoctrine);
        }

        [Test]
        public void BodyLinks_UseV2Ids()
        {
            var handOff = Build(out _);
            Assert.AreEqual(23, handOff.BodyEncounterLinks.Count);
            var bodyIds = new HashSet<string>(LegacyIdMap.Default.All.Where(m => m.Kind == LegacyIdKind.Body && m.V2Id != null).Select(m => m.V2Id), StringComparer.Ordinal);
            foreach (var link in handOff.BodyEncounterLinks)
            {
                Assert.IsTrue(bodyIds.Contains(link.BodyId), link.BodyId);
                Assert.IsTrue(link.BodyId.StartsWith("body_", StringComparison.Ordinal));
                Assert.IsFalse(string.IsNullOrEmpty(link.LegacyEncounterId));
                Assert.Greater(link.Weight, 0);
            }
            var ark = handOff.BodyEncounterLinks.Single(l => l.BodyId == "body_station_ark");
            Assert.AreEqual("demo_station_hub", ark.LegacyEncounterId);
            Assert.AreEqual(1f, ark.Weight);
            Assert.IsTrue(handOff.BodyEncounterLinks.Any(l => l.BodyId == "body_planet_water"));
            Assert.IsTrue(handOff.BodyEncounterLinks.Any(l => l.BodyId == "body_planet_ice"));
            Assert.IsTrue(handOff.BodyEncounterLinks.Any(l => l.BodyId == "body_star"));
            var gate = handOff.BodyEncounterLinks.Single(l => l.BodyId == "body_gate_active");
            Assert.AreEqual("story_use_active_gate", gate.LegacyEncounterId);
            Assert.AreEqual("story_use_active_gate", gate.EncounterIdOverride);
            Assert.IsFalse(handOff.BodyEncounterLinks.Any(l => l.LegacyEncounterId == "event_merchant_shady"), "Fleet narrative associations stay on the fleet.");
        }

        [Test]
        public void StarSystems_PoolsMapped()
        {
            var handOff = Build(out var report);
            Assert.AreEqual(2, handOff.StarSystems.Count);
            var ark = handOff.StarSystems.Single(s => s.LegacySystemId == "ark_start");
            var standard = handOff.StarSystems.Single(s => s.LegacySystemId == "base_solar_system");
            CollectionAssert.AreEqual(new[] { "body_star" }, ark.StarBodyIds);
            CollectionAssert.AreEqual(new[] { "body_star" }, standard.StarBodyIds);
            CollectionAssert.AreEqual(new[] { "base_solar_system" }, ark.ConnectedSystemIds);
            CollectionAssert.AreEqual(new[] { "ark_start" }, standard.ConnectedSystemIds);
            CollectionAssert.AreEqual(new[]
            {
                ("body_planet_barren", 1f, 1), ("body_planet_rocky", 1f, 1), ("body_planet_gas_giant", 1f, 1),
                ("body_station_ark", 1f, 1), ("body_moon", 1f, 1), ("body_derelict_ship", 1f, 2), ("body_station_abandoned", 1f, 1)
            }, ark.EntityPool);
            CollectionAssert.AreEqual(new[]
            {
                ("body_planet_barren", .5f, 2), ("body_planet_rocky", .5f, 2), ("body_planet_water", .2f, 1),
                ("body_planet_terrestrial", .2f, 1), ("body_planet_gas_giant", .4f, 2), ("body_planet_ice_giant", .4f, 2),
                ("body_moon", .5f, 3), ("body_derelict_ship", .4f, 3), ("body_station", .2f, 2),
                ("body_station_abandoned", .2f, 2), ("body_planet_ice", .4f, 2)
            }, standard.EntityPool);
            Assert.AreEqual(ark.LegacySystemId, ark.Dto.SystemId);
            Assert.AreEqual(standard.LegacySystemId, standard.Dto.SystemId);
            Assert.AreEqual(1, report.Entries.Count(e => e.Code == "MIG-I091" && e.AssetPath.EndsWith("/ExtractStablePoint.json", StringComparison.Ordinal)));
            Assert.IsNotNull(handOff.Galaxy);
            Assert.IsNotNull(handOff.OpenWorld);
            Assert.AreEqual(2, handOff.OpenWorld.SolarSystems.Length);
        }
    }
}
