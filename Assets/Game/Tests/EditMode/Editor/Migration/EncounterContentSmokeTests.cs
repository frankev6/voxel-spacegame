using System.Linq;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Validation;
using Game.Editor.Migration.Encounters;
using NUnit.Framework;

namespace Game.Tests.EditMode.Editor.Migration
{
    public sealed class EncounterContentSmokeTests
    {
        [Test] public void AllReferencedIdsExist()
        {
            using var fixture = new EncounterContentTestFixture(); fixture.Factions(); fixture.Fleets();
            var context = fixture.Context(); EncounterContentAuthoring.AuthorRemainingEncounters(context);
            Assert.AreEqual(0, context.Report.ErrorCount, context.Report.ToText()); Assert.AreEqual(6, context.Writer.Outcomes.Count);
            var catalog = fixture.Catalog(); var report = new ValidationReport("ENC-010 scoped acceptance");
            EncounterContentAuthoring.ValidateScoped(catalog, report); Assert.AreEqual(0, report.ErrorCount, report.ToText());
            string[] expected = { "encounter_raiders", "encounter_xeno_probe_tutorial", "encounter_whisper_hunter", "encounter_traveling_merchant",
                "encounter_glass_worms", "encounter_doppelganger", "encounter_scavenger_king", "encounter_jungle_beasts", "encounter_solar_parasite", "encounter_extraction_guardians" };
            CollectionAssert.AreEquivalent(expected, catalog.All<EncounterDefinition>().Select(e => e.IdString));
            foreach (var encounter in catalog.All<EncounterDefinition>())
            {
                Assert.IsNotNull(encounter.Faction); Assert.IsFalse(encounter.UseFactionDoctrine); Assert.IsFalse(encounter.Condition.IsStale);
                foreach (var ship in encounter.Ships) Assert.AreSame(catalog.Get<ShipBlueprintDefinition>(ship.blueprint.Id), ship.blueprint, "Use the actual imported blueprint.");
                if (encounter.Kind == EncounterKind.Story) { Assert.AreEqual(0, encounter.DirectorWeight); Assert.IsTrue(encounter.PersistUntilDefeated); }
            }
            Check("encounter_glass_worms", "faction_xeno", "ship_beetle:3");
            Check("encounter_doppelganger", "faction_xeno", "ship_crescent:1");
            Check("encounter_scavenger_king", "faction_raiders", "ship_bastion:1,ship_fighter:2");
            Check("encounter_jungle_beasts", "faction_xeno", "ship_beetle:4");
            Check("encounter_solar_parasite", "faction_xeno", "ship_butterfly:2");
            Check("encounter_extraction_guardians", "faction_raiders", "ship_striker:2,ship_frigate:1");
            var ark = catalog.Get<FactionDefinition>(new ContentId("faction_ark")); var raiders = catalog.Get<FactionDefinition>(new ContentId("faction_raiders"));
            var xeno = catalog.Get<FactionDefinition>(new ContentId("faction_xeno")); var vestari = catalog.Get<FactionDefinition>(new ContentId("faction_vestari"));
            Assert.IsTrue(ark.IsPlayerFaction); Assert.IsTrue(ark.RelationLocked); Assert.AreEqual("starsys_ark_start", ark.OwnedStarSystems.Single().IdString);
            Assert.AreEqual("starsys_whisper", vestari.OwnedStarSystems.Single().IdString); Assert.AreEqual(6, catalog.All<FactionDefinition>().Count);
            foreach (var hostile in new[] { raiders, xeno }) { Assert.AreEqual(-100, hostile.BasePlayerRelation); Assert.IsTrue(hostile.RelationLocked); Assert.IsFalse(hostile.IsPlayerFaction); }
            Assert.AreEqual("--color-faction-xeno", xeno.MapColorToken); Assert.AreEqual("ai_default", raiders.DefaultAiProfile.IdString);
            CollectionAssert.AreEqual(new[] { "ship_fighter", "ship_striker" }, raiders.Doctrine.Select(d => d.blueprint.IdString));
            CollectionAssert.AreEqual(new[] { 1f, .6f }, raiders.Doctrine.Select(d => d.weight));
            CollectionAssert.AreEquivalent(catalog.All<FactionDefinition>().Where(f => !f.IsPlayerFaction && f != raiders), raiders.Stances.Select(s => s.other));
            Assert.IsTrue(raiders.Stances.All(s => s.stance == FactionStanceValue.Hostile));
            Assert.AreEqual(2, catalog.Get<AuthoredStarSystemDefinition>(new ContentId("starsys_drift")).Danger);
            Assert.IsFalse(fixture.Manifest.TryGetHash(EncounterContentTestFixture.Root + "/Factions/faction_ark.asset", out _), "Filling ownership must not adopt the protected player asset.");
            void Check(string id, string faction, string ships)
            {
                var encounter = catalog.Get<EncounterDefinition>(new ContentId(id)); Assert.AreEqual(faction, encounter.Faction.IdString);
                CollectionAssert.AreEqual(ships.Split(','), encounter.Ships.Select(s => s.blueprint.IdString + ":" + s.countMin));
                Assert.IsTrue(encounter.Ships.All(s => s.countMin == s.countMax));
            }
        }
    }
}
