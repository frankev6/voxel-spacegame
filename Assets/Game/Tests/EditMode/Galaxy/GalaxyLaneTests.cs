using System.Collections.Generic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Generation;
using Game.Tests.EditMode.Data;
using NUnit.Framework;

namespace Game.Tests.EditMode.Galaxy
{
    public sealed class GalaxyLaneTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        internal static GalaxyDef Build(TestGalaxyFixtures.Fixture f)
            => GalaxyDefBuilder.Build(ContentCatalog.Build(f.Definitions, new ValidationReport("Galaxy lanes")), f.Generation.FixedSeed, 1);

        [Test] public void AllReachable()
        {
            using var small = TestGalaxyFixtures.Small; Reachable(small);
            using var large = TestGalaxyFixtures.Large; Reachable(large);
            using var disconnected = TestGalaxyFixtures.Disconnected; Reachable(disconnected);
        }
        static void Reachable(TestGalaxyFixtures.Fixture f)
        {
            using var galaxy = Build(f);
            var seen = new HashSet<uint>(); var queue = new Queue<StarSystemId>();
            seen.Add(galaxy.StartStarSystem.Value); queue.Enqueue(galaxy.StartStarSystem);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                for (int j = 0; j < galaxy.LaneCountOf(id); j++)
                {
                    var neighbour = GalaxyDef.Neighbour(galaxy.Lanes[galaxy.LaneIndexOf(id, j)], id);
                    if (seen.Add(neighbour.Value)) queue.Enqueue(neighbour);
                }
            }
            Assert.That(seen.Count, Is.EqualTo(galaxy.StarSystems.Length));
        }
        [Test] public void Sorted_NoDuplicates()
        {
            using var f = TestGalaxyFixtures.Small; using var galaxy = Build(f);
            ulong previous = 0;
            foreach (var lane in galaxy.Lanes)
            {
                Assert.That(lane.A.Value, Is.LessThan(lane.B.Value));
                ulong key = ((ulong)lane.A.Value << 32) | lane.B.Value;
                Assert.That(key, Is.GreaterThan(previous)); previous = key;
            }
            foreach (var system in galaxy.StarSystems)
            {
                uint previousId = 0;
                for (int j = 0; j < system.LaneCount; j++)
                {
                    uint id = GalaxyDef.Neighbour(galaxy.Lanes[galaxy.LaneIndexOf(system.Id, j)], system.Id).Value;
                    Assert.That(id, Is.GreaterThan(previousId)); previousId = id;
                }
            }
        }
        [Test] public void AuthoredLanes_Kept()
        {
            using var f = TestGalaxyFixtures.Small;
            f.Generation.AuthoredLanes[0].hiddenUntilKnown = true;
            using var galaxy = Build(f);
            foreach (var original in f.Generation.AuthoredLanes)
            {
                var a = StarSystemId.FromAuthored(original.a.Id.Hash); var b = StarSystemId.FromAuthored(original.b.Id.Hash);
                Assert.That(galaxy.TryFindLane(a, b, out int index), Is.True);
                var lane = galaxy.Lanes[index];
                Assert.That(lane.Flags, Is.EqualTo(LaneFlags.Authored | (original.hiddenUntilKnown ? LaneFlags.HiddenUntilKnown : LaneFlags.None)));
                Assert.That(lane.Kind, Is.EqualTo(original.kind));
            }
        }
        [Test] public void LengthAndDegree_Bounds()
        {
            using var f = TestGalaxyFixtures.Small;
            // A dense, connected candidate graph isolates the extra-lane degree rule from later bridge exceptions.
            TestContentFactory.Edit(f.Generation, so =>
            {
                TestContentFactory.Set(so, "extraLaneChance", 0f);
                TestContentFactory.Set(so, "maxLaneLengthLy", 500f);
                TestContentFactory.Set(so, "laneCandidateNeighbours", 16);
            });
            using var forest = Build(f);
            TestContentFactory.Edit(f.Generation, so => TestContentFactory.Set(so, "extraLaneChance", 1f));
            using var full = Build(f);
            var degrees = new Dictionary<uint, int>();
            foreach (var s in full.StarSystems) degrees.Add(s.Id.Value, 0);
            int longMandatory = 0, excessMandatory = 0;
            foreach (var lane in forest.Lanes)
            {
                if ((lane.Flags & LaneFlags.Authored) != 0) continue;
                degrees[lane.A.Value]++; degrees[lane.B.Value]++;
                if (lane.LengthLy > f.Generation.MaxLaneLengthLy) longMandatory++;
            }
            foreach (var degree in degrees.Values) if (degree > f.Generation.MaxLaneDegree) excessMandatory++;
            foreach (var lane in full.Lanes)
            {
                if (forest.TryFindLane(lane.A, lane.B, out _)) continue;
                Assert.That(lane.LengthLy, Is.LessThanOrEqualTo(f.Generation.MaxLaneLengthLy));
                Assert.That(degrees[lane.A.Value]++, Is.LessThan(f.Generation.MaxLaneDegree));
                Assert.That(degrees[lane.B.Value]++, Is.LessThan(f.Generation.MaxLaneDegree));
            }
            TestContext.WriteLine($"Mandatory bridge length exceptions: {longMandatory}; mandatory degree exceptions: {excessMandatory}");
        }
    }
}
