using Game.Data.Definitions;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    /// <summary>ENC-002: every authored config default matches SPEC-33 §4.1.</summary>
    public sealed class EncounterConfigTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        [Test] public void Defaults()
        {
            var config = TestContentFactory.Create<EncounterConfigDefinition>("config_encounters");
            Assert.AreEqual(90f, config.FirstRollDelaySeconds); Assert.AreEqual(120f, config.RollIntervalSeconds);
            CollectionAssert.AreEqual(new[] { 0f, .25f, .45f, .65f }, config.DangerSpawnChance);
            Assert.AreEqual(2, config.MaxActiveDirectorInstances); Assert.AreEqual(800f, config.MinPlayerDistanceM);
            Assert.AreEqual(300f, config.CombatMinDistanceM); Assert.AreEqual(10f, config.ArrivalGraceSeconds);
            Assert.AreEqual(2000f, config.ArrivalGraceDistanceM); Assert.AreEqual(12, config.PlacementTries);
            Assert.AreEqual(30f, config.PlacementStepDegrees); Assert.AreEqual(150f, config.BodyClearanceM);
            Assert.AreEqual(6000f, config.DespawnDistanceM); Assert.AreEqual(30f, config.DespawnDelaySeconds);
            Assert.AreEqual(16, config.MaxInstances); Assert.AreEqual(40, config.MaxShipsAlive);
            Assert.AreEqual(-25, config.HostileAtOrBelow); Assert.AreEqual(50, config.FriendlyAtOrAbove);
            Assert.AreEqual(10, config.ProvokePenalty); Assert.AreEqual(60, config.ProvokeIntervalTicks);
            Assert.AreEqual(300f, config.HailDistanceM);
            var report = TestContentFactory.Validate(config); Assert.AreEqual(0, report.Entries.Count, report.ToText());
        }
    }
}
