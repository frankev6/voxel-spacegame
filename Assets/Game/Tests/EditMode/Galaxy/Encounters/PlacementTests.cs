using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Game.Galaxy.Encounters;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Galaxy.Encounters
{
    public sealed class PlacementTests
    {
        const ulong Seed = 42;
        static PlacementParams Params => new PlacementParams
        {
            DistanceMinM = 1000, DistanceMaxM = 1400, MinPlayerDistanceM = 500,
            BodyClearanceM = 100, StepDegrees = 90, Tries = 4
        };

        static double3 First(in PlacementParams p, double3 anchor, float radius = 0)
        {
            var rng = RngStreams.For(Seed, RngPurpose.EncounterPlacement, 0);
            double distance = (double)radius + math.lerp(p.DistanceMinM, p.DistanceMaxM, rng.NextFloat());
            double angle = 2 * System.Math.PI * rng.NextFloat();
            return anchor + distance * new double3(math.cos(angle), 0, math.sin(angle));
        }

        [Test] public void PlayerDistance()
        {
            using var content = new StarSystemContent(new StarSystemId(1));
            var p = Params; var anchor = new double3(1e9, 7, -1e9);
            var blockedFirst = First(p, anchor, 250);
            Assert.That(EncounterPlacement.TryPlaceCentre(content, anchor, 250, blockedFirst, 0, Seed, p, out var centre), Is.True);
            Assert.That(math.length((centre - blockedFirst).xz), Is.GreaterThanOrEqualTo(p.MinPlayerDistanceM));
            Assert.That(centre.y, Is.EqualTo(anchor.y));
            Assert.That(math.length((centre - anchor).xz), Is.InRange(1250d - 1e-6, 1650d + 1e-6));
            Assert.That(EncounterPlacement.TryPlaceCentre(content, anchor, 250, blockedFirst, 0, Seed, p, out var repeat), Is.True);
            Assert.That(repeat, Is.EqualTo(centre));
            p.Tries = 1;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, anchor, 250, blockedFirst + new double3(0, 1e6, 0), 0, Seed, p, out _), Is.False);
        }

        [Test] public void BodyClearance()
        {
            using var content = new StarSystemContent(new StarSystemId(1));
            var p = Params; var first = First(p, double3.zero);
            // A moving moon with a parent orbit blocks the first candidate through its clearance margin.
            const double time = 25;
            var body = new BodySpec
            {
                Id = new BodyId(content.Id, BodyRole.Moon, 0, 0), RadiusM = 200,
                ParentOrbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = 300, PhaseRad = .4 },
                Orbit = new OrbitElements { Mode = OrbitMode.Circular, PeriodS = 100 }
            };
            var local = first + new double3(250, 0, 0) - OrbitMath.LocalPosition(body.ParentOrbit, time);
            body.Orbit.RadiusM = math.length(local.xz);
            body.Orbit.PhaseRad = math.atan2(local.z, local.x) - System.Math.PI / 2;
            content.Bodies.Add(body);
            Assert.That(math.distance(OrbitMath.Position(body, time), first), Is.EqualTo(250d).Within(1e-8));
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, time, Seed, p, out var centre), Is.True);
            Assert.That(math.length((centre - OrbitMath.Position(body, time)).xz), Is.GreaterThanOrEqualTo(body.RadiusM + p.BodyClearanceM));
            p.Tries = 1;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, time, Seed, p, out _), Is.False);
            // At another orbit time the first candidate is free; destroyed bodies are also ignored.
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, time + 50, Seed, p, out centre), Is.True);
            Assert.That(centre, Is.EqualTo(first));
            body.State = BodyStateFlags.Destroyed; content.Bodies[0] = body;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, time, Seed, p, out centre), Is.True);
            Assert.That(centre, Is.EqualTo(first));
        }

        [Test] public void BeltAvoided()
        {
            using var content = new StarSystemContent(new StarSystemId(1));
            var p = Params; var anchor = new double3(1500, 3, 0);
            double radius = math.length(First(p, anchor).xz);
            var belt = new BodySpec
            {
                Id = new BodyId(content.Id, BodyRole.Belt, 0, 0),
                // First candidate is beyond the physical outer edge but inside its clearance margin.
                BeltInnerRadiusM = (float)radius - 30, BeltOuterRadiusM = (float)radius - 20
            };
            content.Bodies.Add(belt);
            p.Tries = 1;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, anchor, 0, anchor, 0, Seed, p, out _), Is.False);
            p.Tries = 4;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, anchor, 0, anchor, 0, Seed, p, out var centre), Is.True);
            double placedRadius = math.length(centre.xz);
            Assert.That(placedRadius < belt.BeltInnerRadiusM - p.BodyClearanceM || placedRadius > belt.BeltOuterRadiusM + p.BodyClearanceM, Is.True);
            belt.State = BodyStateFlags.Destroyed; content.Bodies[0] = belt;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, anchor, 0, anchor, 0, Seed, p, out centre), Is.True);
            Assert.That(centre, Is.EqualTo(First(p, anchor)));
        }

        [Test] public void AllBlocked_False()
        {
            using var content = new StarSystemContent(new StarSystemId(1));
            var p = Params;
            content.Bodies.Add(new BodySpec { Id = new BodyId(content.Id, BodyRole.Star, 0, 0), RadiusM = 2000 });
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, 0, Seed, p, out var centre), Is.False);
            Assert.That(centre, Is.EqualTo(double3.zero));
            content.Bodies.Clear();
            content.Bodies.Add(new BodySpec
            {
                Id = new BodyId(content.Id, BodyRole.Belt, 0, 0), BeltInnerRadiusM = 900, BeltOuterRadiusM = 1500
            });
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, 0, Seed, p, out _), Is.False);
            content.Bodies.Clear(); p.MinPlayerDistanceM = 1500;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, 0, Seed, p, out _), Is.False);
            p.Tries = 0;
            Assert.That(EncounterPlacement.TryPlaceCentre(content, double3.zero, 0, double3.zero, 0, Seed, p, out _), Is.False);
        }

        [Test] public void LineAbreast_Geometry()
        {
            var centre = new double3(1e9, 11, -1e9);
            var ships = new List<PlannedShip> { new PlannedShip { RadiusM = 5 }, new PlannedShip { RadiusM = 30 }, new PlannedShip { RadiusM = 10 } };
            var poses = new List<(double3 pos, float yaw)> { default };
            var repeat = new List<(double3 pos, float yaw)>();
            var forward = new double3(.6, 0, .8); var right = new double3(.8, 0, -.6);
            EncounterPlacement.ShipPoses(centre, centre + forward * 100, ships, 0, null, poses);
            EncounterPlacement.ShipPoses(centre, centre + forward * 100, ships, 0, null, repeat);
            CollectionAssert.AreEqual(poses, repeat);
            Assert.That(poses.Count, Is.EqualTo(3));
            for (int i = 0; i < poses.Count; i++)
            {
                Assert.That(math.distance(poses[i].pos, centre + right * ((i - 1) * 80)), Is.LessThan(1e-6));
                Assert.That(poses[i].yaw, Is.EqualTo((float)math.atan2(.6, .8)).Within(1e-6));
                Assert.That(poses[i].pos.y, Is.EqualTo(centre.y));
                if (i > 0) Assert.That(math.distance(poses[i].pos, poses[i - 1].pos), Is.GreaterThanOrEqualTo(80d - 1e-6));
            }
            Assert.That(math.distance((poses[0].pos + poses[2].pos) / 2, centre), Is.LessThan(1e-6));
            // Even fleet, authored spacing, and zero XZ face direction (+Z fallback).
            ships.RemoveAt(2);
            EncounterPlacement.ShipPoses(centre, centre + new double3(0, 5, 0), ships, 120, null, poses);
            Assert.That(poses[0].pos, Is.EqualTo(centre - new double3(60, 0, 0)));
            Assert.That(poses[1].pos, Is.EqualTo(centre + new double3(60, 0, 0)));
            Assert.That(poses[0].yaw, Is.Zero); Assert.That(poses[1].yaw, Is.Zero);
            ships.Clear();
            EncounterPlacement.ShipPoses(centre, centre, ships, 0, null, poses);
            Assert.That(poses, Is.Empty);
        }
    }
}
