using System.Linq;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Game.Galaxy.Generation;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class StarSystemContentTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        static StarSystemContent Generate(GalaxyDef galaxy, StarSystemId id)
        {
            var result = StarSystemContentBuilder.Build(galaxy, id, out bool warning, out bool error);
            Assert.That(warning || error, Is.False);
            return result;
        }
        [Test] public void Deterministic()
        {
            using var fixture = TestGalaxyFixtures.Small; using var galaxy = GalaxyLaneTests.Build(fixture);
            using var first = Generate(galaxy, galaxy.StarSystems[0].Id);
            using var second = Generate(galaxy, galaxy.StarSystems[0].Id);
            CollectionAssert.AreEqual(first.Bodies.AsArray().ToArray(), second.Bodies.AsArray().ToArray());
            Assert.That(second.GeneratedHash, Is.EqualTo(first.GeneratedHash));
        }
        [Test] public void Sorted_ByLocalKey()
        {
            using var fixture = TestGalaxyFixtures.Small; using var galaxy = GalaxyLaneTests.Build(fixture);
            foreach (var system in galaxy.StarSystems)
            {
                using var content = Generate(galaxy, system.Id); ulong previous = 0;
                foreach (var body in content.Bodies)
                {
                    Assert.That(body.Id.LocalKey, Is.GreaterThan(previous)); previous = body.Id.LocalKey;
                    Assert.That(content.TryFind(body.Id.LocalKey, out int index), Is.True);
                    Assert.That(content.Bodies[index].Id, Is.EqualTo(body.Id));
                }
            }
        }
        [Test] public void Orbits_Increasing()
        {
            using var fixture = TestGalaxyFixtures.Small;
            TestContentFactory.Edit(fixture.Planet, so => TestContentFactory.Set(so, "canHaveMoons", true));
            TestContentFactory.Edit(fixture.Template, so =>
            {
                TestContentFactory.Set(so, "moonChance", 1f);
                TestContentFactory.Set(so, "minStations", 2); TestContentFactory.Set(so, "maxStations", 2);
                TestContentFactory.Set(so, "stationRadiusMinM", 20000f); TestContentFactory.Set(so, "stationRadiusMaxM", 80000f);
                TestContentFactory.Set(so, "pointOfInterestRadiusMinM", 20000f); TestContentFactory.Set(so, "pointOfInterestRadiusMaxM", 80000f);
            });
            using var galaxy = GalaxyLaneTests.Build(fixture); int moons = 0;
            foreach (var system in galaxy.StarSystems)
            {
                if (system.AuthoredIndex != 0) continue;
                using var content = Generate(galaxy, system.Id); double previous = 0;
                foreach (var body in content.Bodies)
                {
                    if (body.Id.Role == BodyRole.Planet) { Assert.That(body.Orbit.RadiusM, Is.GreaterThan(previous)); previous = body.Orbit.RadiusM; }
                    if (body.Id.Role == BodyRole.Moon)
                    {
                        moons++; Assert.That(content.TryFind(body.ParentKey, out int index), Is.True);
                        Assert.That(content.Bodies[index].Id.Role, Is.EqualTo(BodyRole.Planet));
                        Assert.That(body.ParentOrbit, Is.EqualTo(content.Bodies[index].Orbit));
                        Assert.That(body.Orbit.RadiusM, Is.GreaterThan(content.Bodies[index].RadiusM));
                    }
                }
                var fixedBodies = content.Bodies.AsArray().ToArray().Where(x => x.Id.Role == BodyRole.Station || x.Id.Role == BodyRole.PointOfInterest).ToArray();
                for (int i = 0; i < fixedBodies.Length; i++)
                    for (int j = 0; j < i; j++)
                        Assert.That(math.distance(OrbitMath.Position(fixedBodies[i], 0), OrbitMath.Position(fixedBodies[j], 0)), Is.GreaterThanOrEqualTo(fixture.Template.MinBodySeparationM));
            }
            Assert.That(moons, Is.GreaterThan(0));
        }
        [Test] public void Gates_OnRing_Separated()
        {
            using var fixture = new TestGalaxyFixtures.Fixture(7, 0, 0x1234, false);
            for (int i = 0; i < 7; i++)
            {
                var system = fixture.Generation.AuthoredStarSystems[i];
                int n = i;
                TestContentFactory.Edit(system, so =>
                {
                    TestContentFactory.Set(so, "connectProcedurally", false);
                    double angle = math.radians(n * 5.0);
                    so.FindProperty("positionLy").vector2Value = n == 0 ? UnityEngine.Vector2.zero : new UnityEngine.Vector2((float)(40 * math.cos(angle)), (float)(40 * math.sin(angle)));
                });
                if (i > 0) { fixture.Generation.AuthoredLanes[i - 1].a = fixture.Start; fixture.Generation.AuthoredLanes[i - 1].b = system; }
            }
            using var galaxy = GalaxyLaneTests.Build(fixture); using var content = Generate(galaxy, galaxy.StartStarSystem);
            var gates = content.Bodies.AsArray().ToArray().Where(x => x.Id.Role == BodyRole.LaneGate).ToArray();
            Assert.That(gates.Length, Is.EqualTo(6));
            for (int i = 0; i < gates.Length; i++)
            {
                Assert.That(gates[i].Orbit.RadiusM, Is.EqualTo(content.LaneRingRadiusM));
                for (int j = 0; j < i; j++)
                {
                    double d = gates[i].Orbit.PhaseRad - gates[j].Orbit.PhaseRad;
                    double arc = math.abs(math.atan2(math.sin(d), math.cos(d))) * content.LaneRingRadiusM;
                    Assert.That(arc, Is.GreaterThanOrEqualTo(fixture.Template.MinBodySeparationM - 1e-8));
                }
            }
        }
        [Test] public void Authored_MatchesData()
        {
            using var fixture = TestGalaxyFixtures.AuthoredOnly;
            fixture.Start.Bodies[0].radiusOverrideM = 333;
            fixture.Start.Bodies[0].phaseDegrees = 42;
            using var galaxy = GalaxyLaneTests.Build(fixture); using var content = Generate(galaxy, galaxy.StartStarSystem);
            int authoredIndex = galaxy.Get(galaxy.StartStarSystem).AuthoredIndex - 1;
            var range = galaxy.AuthoredBodyRanges[authoredIndex];
            for (int i = range.x; i < range.x + range.y; i++)
            {
                var source = galaxy.AuthoredBodies[i]; Assert.That(content.TryFind(source.LocalKey, out int index), Is.True);
                var body = content.Bodies[index];
                Assert.That(body.ParentKey, Is.EqualTo(source.ParentKey)); Assert.That(body.TemplateIndex, Is.EqualTo(source.TemplateIndex));
                Assert.That(body.Orbit.Mode, Is.EqualTo(source.Mode)); Assert.That(body.Orbit.RadiusM, Is.EqualTo(source.OrbitRadiusM));
                Assert.That(body.Orbit.PhaseRad, Is.EqualTo(source.PhaseRad)); Assert.That(body.YawRad, Is.EqualTo(source.YawRad));
                if (source.RadiusOverrideM > 0) Assert.That(body.RadiusM, Is.EqualTo(source.RadiusOverrideM));
            }
        }
        [Test] public void Regenerate_NoHiddenState()
        {
            using var fixture = TestGalaxyFixtures.Small; using var galaxy = GalaxyLaneTests.Build(fixture);
            using var baseline = Generate(galaxy, galaxy.StarSystems[0].Id);
            for (int i = 0; i < 100; i++)
            { using var content = Generate(galaxy, baseline.Id); Assert.That(content.GeneratedHash, Is.EqualTo(baseline.GeneratedHash)); }
        }
    }
}
