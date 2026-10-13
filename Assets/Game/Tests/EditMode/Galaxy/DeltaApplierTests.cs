using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Generation;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class DeltaApplierTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static StarSystemContent Content(GalaxyDef galaxy)
            => StarSystemContentBuilder.Build(galaxy, galaxy.StartStarSystem, out _, out _);
        static ContentCatalog Catalog(TestGalaxyFixtures.Fixture fixture)
            => ContentCatalog.Build(fixture.Definitions, new ValidationReport("Delta application"));
        [Test] public void Generated_Unchanged()
        {
            using var fixture = new TestGalaxyFixtures.Fixture(1, 0, 42, false);
            using var galaxy = GalaxyLaneTests.Build(fixture); using var content = Content(galaxy);
            var before = content.Bodies.AsArray().ToArray(); ulong hash = content.GeneratedHash;
            using var delta = new StarSystemDelta(content.Id);
            delta.AddStorySpawn(new StorySpawnRecord { Template = fixture.Station.Id, OffsetM = new double3(30, 999, 40), YawRad = 1 });
            StarSystemDeltaApplier.Apply(content, delta, galaxy, Catalog(fixture));
            Assert.AreEqual(hash, content.GeneratedHash); Assert.AreEqual(before.Length + 1, content.Bodies.Length);
            for (int i = 0; i < before.Length; i++) Assert.AreEqual(before[i], content.Bodies[i]);
            var story = content.Bodies[before.Length]; Assert.AreEqual(BodyRole.StorySpawn, story.Id.Role);
            Assert.AreEqual(50, story.Orbit.RadiusM); Assert.AreEqual(math.atan2(40d, 30d), story.Orbit.PhaseRad);
            Assert.AreEqual(BodyStateFlags.StorySpawn, story.State); Assert.AreEqual(1, story.YawRad);
        }
        [Test] public void Destroy_IsolatesBody()
        {
            using var fixture = new TestGalaxyFixtures.Fixture(1, 0, 42, false);
            using var galaxy = GalaxyLaneTests.Build(fixture); using var content = Content(galaxy);
            var before = content.Bodies.AsArray().ToArray(); ulong hash = content.GeneratedHash;
            using var delta = new StarSystemDelta(content.Id); delta.MarkDestroyed(before[0].Id.LocalKey);
            StarSystemDeltaApplier.Apply(content, delta, galaxy, Catalog(fixture));
            Assert.AreEqual(hash, content.GeneratedHash);
            before[0].State |= BodyStateFlags.Destroyed;
            CollectionAssert.AreEqual(before, content.Bodies.AsArray().ToArray());
        }
        [Test] public void Replacement_SetsTemplate()
        {
            using var fixture = new TestGalaxyFixtures.Fixture(1, 0, 42, false);
            using var galaxy = GalaxyLaneTests.Build(fixture); using var content = Content(galaxy);
            var before = content.Bodies.AsArray().ToArray(); ulong hash = content.GeneratedHash;
            using var delta = new StarSystemDelta(content.Id); delta.SetReplacement(before[0].Id.LocalKey, fixture.Station.Id);
            StarSystemDeltaApplier.Apply(content, delta, galaxy, Catalog(fixture));
            before[0].TemplateIndex = galaxy.BodyTemplateIndexOf(fixture.Station); before[0].State |= BodyStateFlags.Replaced;
            CollectionAssert.AreEqual(before, content.Bodies.AsArray().ToArray()); Assert.AreEqual(hash, content.GeneratedHash);
        }
        [Test] public void UnresolvedTemplate_Reported()
        {
            using var fixture = new TestGalaxyFixtures.Fixture(1, 0, 42, false);
            using var galaxy = GalaxyLaneTests.Build(fixture); using var content = Content(galaxy);
            var before = content.Bodies.AsArray().ToArray(); var catalog = Catalog(fixture);
            using var delta = new StarSystemDelta(content.Id);
            delta.SetReplacement(before[0].Id.LocalKey, new ContentId("body_missing_replacement"));
            delta.AddStorySpawn(new StorySpawnRecord { Template = new ContentId("body_missing_spawn") });
            StarSystemDeltaApplier.Apply(content, delta, galaxy, catalog);
            CollectionAssert.AreEqual(before, content.Bodies.AsArray().ToArray()); Assert.AreEqual(2, catalog.Unresolved.Count);
        }
    }
}
