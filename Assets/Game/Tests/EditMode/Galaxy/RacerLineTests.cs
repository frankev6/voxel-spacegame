using System.Collections.Generic;
using Game.Data.Definitions;
using Game.Galaxy.Racing;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;

namespace Game.Tests.EditMode.Galaxy
{
    /// <summary>RACE-017: the racing line on Track 01, and the racer AI's plan over it.</summary>
    public sealed class RacerLineTests : GameTestBase
    {
        const string TrackPath = "Assets/Game/Content/Racing/track_01.asset";
        EcsTestWorld world;
        EntityManager EM => world.EntityManager;
        TrackDefinition track;
        Entity race;

        [SetUp]
        public void SetUp()
        {
            track = AssetDatabase.LoadAssetAtPath<TrackDefinition>(TrackPath);
            Assert.IsNotNull(track, TrackPath);
            world = CreateWorld();
            race = EM.CreateEntity(); EM.AddBuffer<RaceGate>(race); EM.AddBuffer<RaceLeg>(race); EM.AddBuffer<RaceLegPoint>(race);
            var gates = EM.GetBuffer<RaceGate>(race);
            foreach (var g in track.Gates)
            {
                ulong mask = 0; foreach (int n in g.Next) mask |= 1UL << n;
                gates.Add(new RaceGate { Position = g.Position, Yaw = g.Yaw, Width = g.Width, NextMask = mask });
            }
            var legs = EM.GetBuffer<RaceLeg>(race); var points = EM.GetBuffer<RaceLegPoint>(race);
            foreach (var leg in RaceLines.Build(track))
            {
                legs.Add(new RaceLeg { From = (byte)leg.From, To = (byte)leg.To, Start = points.Length, Count = leg.Points.Count, Length = leg.Length });
                foreach (var (p, half) in leg.Points)
                {
                    float2 push = RaceLines.Push(track, p);
                    points.Add(new RaceLegPoint { Position = p, HalfWidth = half, PushSpeed = push.x, PushAccel = push.y });
                }
            }
        }
        [TearDown] public void TearDown() => DisposeTrackedWorlds();
        // a mid-weight racer (strafe 15 m/s², reverse 15 m/s², yaw cap 120°/s)
        static RacerPlan.Ability Racer(float speed) => new RacerPlan.Ability { Speed = speed, Lateral = 13, Brake = 12, YawCap = math.radians(120) };

        [Test]
        public void Track01_EveryGateEdge_HasALegAlongTheRoutes()
        {
            var legs = RaceLines.Build(track);
            for (int g = 0; g < track.Gates.Length; g++)
                foreach (int to in track.Gates[g].Next)
                {
                    var leg = legs.Find(l => l.From == g && l.To == to);
                    Assert.IsNotNull(leg.Points, $"no leg {g} → {to}");
                    float2 a = track.Gates[g].Position, b = track.Gates[to].Position;
                    Assert.Less(math.distance(leg.Points[0].position, a), 8, $"{g} → {to} starts at gate {g}");
                    Assert.Less(math.distance(leg.Points[^1].position, b), 8, $"{g} → {to} ends at gate {to}");
                    Assert.GreaterOrEqual(leg.Length, math.distance(a, b) - 16, $"{g} → {to} is no shorter than straight (gates sit up to 8 m off the route points)");
                    Assert.Less(leg.Length, math.distance(a, b) * 4 + 200, $"{g} → {to} does not wander");
                    for (int i = 1; i < leg.Points.Count; i++)
                        Assert.Less(math.distance(leg.Points[i - 1].position, leg.Points[i].position), RaceLines.JoinDistance + 15, $"{g} → {to} has no jump at {i}");
                }
        }

        [Test]
        public void Plan_FromTheGrid_StartsBehindGate0AndRunsThroughAllowedGates()
        {
            var line = new List<RacerLinePoint>();
            RacerPlan.Build(line, EM.GetBuffer<RaceGate>(race), EM.GetBuffer<RaceLeg>(race), EM.GetBuffer<RaceLegPoint>(race), -1, 1, 1, 0, Racer(0));
            Assert.Greater(line.Count, 100);
            var g0 = track.Gates[0];
            float2 behind = (float2)g0.Position - new float2(math.sin(g0.Yaw), math.cos(g0.Yaw)) * RacerPlan.GridApproach;
            Assert.Less(math.distance(line[0].Position, behind), 1, "the line starts behind the start line");
            // the gates it passes, in order, each one a valid next of the one before
            var passed = new List<int>();
            foreach (var p in line)
                for (int g = 0; g < track.Gates.Length; g++)
                    if (math.distance(p.Position, track.Gates[g].Position) < 8 && (passed.Count == 0 || passed[^1] != g)) passed.Add(g);
            Assert.AreEqual(0, passed[0], "gate 0 first");
            Assert.GreaterOrEqual(passed.Count, RacerPlan.LegsAhead + 1);
            for (int i = 1; i < passed.Count; i++)
                Assert.IsTrue(System.Array.IndexOf(track.Gates[passed[i - 1]].Next, passed[i]) >= 0 || IsOnTheWay(passed, i),
                    $"gate {passed[i]} may follow gate {passed[i - 1]}");
            foreach (var p in line) Assert.Greater(p.Radius, 1, "every point has a turn radius");
        }
        // a leg may run through a gate that is not its end (3 → 5 passes 13 and 4); those are allowed next gates too
        bool IsOnTheWay(List<int> passed, int i) => i + 1 < passed.Count && System.Array.IndexOf(track.Gates[passed[i - 1]].Next, passed[i + 1]) >= 0;

        [Test]
        public void Choose_ASkilledRacerTakesTheShortestWay_AWeakOneVaries()
        {
            var legs = EM.GetBuffer<RaceLeg>(race);
            var toFinish = RacerPlan.ToFinish(legs, track.Gates.Length); var points = EM.GetBuffer<RaceLegPoint>(race);
            int fork = System.Array.FindIndex(track.Gates, g => g.Next.Length > 2);
            Assert.GreaterOrEqual(fork, 0, "Track 01 has a three-way fork");
            int shortest = RacerPlan.Choose(legs, points, toFinish, fork, 1, 1, 0, Racer(60));
            var seen = new HashSet<int>();
            for (uint seed = 0; seed < 64; seed++)
            {
                Assert.AreEqual(shortest, RacerPlan.Choose(legs, points, toFinish, fork, seed, 1, 0, Racer(60)), "skill 1 always takes the shortest");
                seen.Add(RacerPlan.Choose(legs, points, toFinish, fork, seed, 0, 0, Racer(60)));
            }
            Assert.Greater(seen.Count, 1, "skill 0 sometimes takes another branch");
        }
    

        [Test]
        public void Choose_NoBranchItCannotCorner_TheBreachSOnTheBoostStrip()
        {
            var legs = EM.GetBuffer<RaceLeg>(race); var points = EM.GetBuffer<RaceLegPoint>(race);
            var toFinish = RacerPlan.ToFinish(legs, track.Gates.Length);
            int main = -1, breach = -1;
            for (int i = 0; i < legs.Length; i++) { if (legs[i].From == 0 && legs[i].To == 1) main = i; if (legs[i].From == 0 && legs[i].To == 10) breach = i; }
            Assert.IsTrue(main >= 0 && breach >= 0, "gate 0 forks to 1 (main) and 10 (breach)");
            bool pushed = false;
            for (int k = legs[breach].Start; k < legs[breach].Start + legs[breach].Count; k++) pushed |= points[k].PushSpeed > 0;
            Assert.IsTrue(pushed, "the breach leg starts on the boost strip");
            Assert.IsFalse(RacerPlan.Feasible(points, legs[breach], Racer(150)), "the breach S at 150 m/s on the strip is not makeable");
            Assert.Less(RacerPlan.Excess(points, legs[main], Racer(150)), RacerPlan.Excess(points, legs[breach], Racer(150)) * .7f, "main is far closer to makeable");
            for (uint seed = 0; seed < 64; seed++)
                Assert.AreEqual(main, RacerPlan.Choose(legs, points, toFinish, 0, seed, 0, 0, Racer(150)), "even a weak racer stays on main at speed");
        }
    }
}
