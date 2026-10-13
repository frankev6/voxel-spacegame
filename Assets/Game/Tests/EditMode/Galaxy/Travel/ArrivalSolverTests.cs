using System;
using System.Diagnostics;
using System.IO;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Galaxy.Travel;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    public sealed class ArrivalSolverTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();

        const int Lane = 4;
        const double Ring = 10000, ArrivalRadius = 9730;
        static ArrivalParams Parameters => new() { StandoffM = 150, StepM = 100, ClearanceM = 60, MaxSteps = 16 };

        static StarSystemContent Content(bool gate = true)
        {
            var content = new StarSystemContent(StarSystemId.FromProceduralIndex(6)) { LaneRingRadiusM = Ring };
            if (gate)
            {
                var body = Fixed(content, BodyRole.LaneGate, 0, new double3(Ring, 0, 0), 120);
                body.LaneIndex = Lane; Add(content, body);
            }
            return content;
        }
        static BodySpec Fixed(StarSystemContent content, BodyRole role, ushort slot, double3 position, float radius) => new()
        {
            Id = new BodyId(content.Id, role, slot, 0), RadiusM = radius,
            Orbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = math.length(position.xz), PhaseRad = math.atan2(position.z, position.x) },
            ParentOrbit = new OrbitElements { Mode = OrbitMode.Fixed }, LaneIndex = ushort.MaxValue
        };
        static void Add(StarSystemContent content, BodySpec body)
        {
            // Keep the canonical LocalKey order, including when a test adds a body before the existing gate.
            int index = content.Bodies.Length; content.Bodies.Add(body);
            while (index > 0 && content.Bodies[index - 1].Id.LocalKey > body.Id.LocalKey)
            { content.Bodies[index] = content.Bodies[index - 1]; index--; }
            content.Bodies[index] = body;
        }
        static ArrivalPlan Jump(StarSystemContent content, in ArrivalParams p, double time = 0) =>
            ArrivalSolver.SolveJump(content, Lane, double2.zero, new double2(1, 0), time, p);
        static double3 Candidate(double radius, int k, float step)
        {
            int signedStep = (k + 1) / 2 * ((k & 1) != 0 ? 1 : -1);
            double theta = signedStep * (double)step / radius;
            return new double3(radius * math.cos(theta), 0, radius * math.sin(theta));
        }
        static void Near(double3 expected, double3 actual) => Assert.That(math.distance(expected, actual), Is.LessThan(1e-8));
        static void Faces(ArrivalPlan plan, double3 centre)
        {
            var forward = math.mul(quaternion.RotateY(plan.YawRad), new float3(0, 0, 1));
            Assert.That(math.dot((double3)forward, math.normalize(centre - plan.PositionM)), Is.GreaterThan(.999));
        }
        static byte[] Bits(ArrivalPlan plan)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(plan.StarSystem.Value);
            writer.Write(plan.PositionM.x); writer.Write(plan.PositionM.y); writer.Write(plan.PositionM.z);
            writer.Write(plan.YawRad); writer.Write(plan.GateKey);
            writer.Write(plan.GatePositionM.x); writer.Write(plan.GatePositionM.y); writer.Write(plan.GatePositionM.z);
            writer.Write(plan.GateRadiusM); writer.Write(plan.Adjusted); writer.Write(plan.Obstructed);
            writer.Flush(); return stream.ToArray();
        }

        [Test] public void Gate_PointAndFacing()
        {
            using var content = Content(); var p = Parameters;
            var plan = Jump(content, p);
            Assert.AreEqual(content.Id, plan.StarSystem);
            Assert.AreEqual(new BodyId(content.Id, BodyRole.LaneGate, 0, 0).LocalKey, plan.GateKey);
            Near(new double3(Ring, 0, 0), plan.GatePositionM); Assert.AreEqual(120, plan.GateRadiusM);
            Assert.That(math.distance(plan.PositionM, plan.GatePositionM), Is.EqualTo(270).Within(1e-8));
            Near(new double3(ArrivalRadius, 0, 0), plan.PositionM); Faces(plan, double3.zero);
            Assert.That(math.length(plan.PositionM.xz), Is.LessThan(Ring));
            Assert.AreEqual(0, plan.Adjusted); Assert.AreEqual(0, plan.Obstructed);

            var target = new double3(-4000, 0, 6000);
            var cruise = ArrivalSolver.SolveCruise(content, target - new double3(2000, 0, 0), target, 100, 0, p);
            Near(target - new double3(250, 0, 0), cruise.PositionM); Faces(cruise, target);
            Assert.AreEqual(content.Id, cruise.StarSystem); Assert.AreEqual(0ul, cruise.GateKey);
            Near(target, cruise.GatePositionM); Assert.AreEqual(100, cruise.GateRadiusM);
            Assert.AreEqual(0, cruise.Adjusted); Assert.AreEqual(0, cruise.Obstructed);

            // Exercise the same solver against actual generated content, without a runtime travel double.
            using var fixture = new TestGalaxyFixtures.Fixture(2, 0, 42, false);
            using var world = new World("Arrival solver generated content"); using var state = new GalaxyState();
            var catalog = ContentCatalog.Build(fixture.Definitions, new ValidationReport("Arrival solver"));
            using var service = new GalaxyService(catalog, state, world, new GalaxyFrameTicker()); service.NewGame(42);
            var generated = service.GetContent(service.Galaxy.StartStarSystem);
            int lane = service.Galaxy.LaneIndexOf(generated.Id, 0);
            var source = GalaxyDef.Neighbour(service.Galaxy.Lanes[lane], generated.Id);
            var actual = ArrivalSolver.SolveJump(generated, lane, service.Galaxy.Get(generated.Id).PositionLy,
                service.Galaxy.Get(source).PositionLy, 0, p);
            Assert.AreEqual(generated.Id, actual.StarSystem);
            Assert.IsTrue(generated.TryFind(actual.GateKey, out int gateIndex));
            var gate = generated.Bodies[gateIndex];
            Assert.AreEqual(BodyRole.LaneGate, gate.Id.Role); Assert.AreEqual(lane, gate.LaneIndex);
            Near(OrbitMath.Position(gate, 0), actual.GatePositionM); Assert.AreEqual(gate.RadiusM, actual.GateRadiusM);
            Assert.AreEqual(0, actual.Obstructed); Faces(actual, double3.zero);
        }

        [Test] public void Obstruction_StepsAlongRing()
        {
            using var content = Content(); var p = Parameters; p.ClearanceM = 0;
            Add(content, Fixed(content, BodyRole.Station, 0, new double3(ArrivalRadius, 0, 0), 20));
            for (int k = 1; k <= 3; k++)
            {
                var plan = Jump(content, p);
                Near(Candidate(ArrivalRadius, k, p.StepM), plan.PositionM);
                Assert.AreEqual(1, plan.Adjusted); Assert.AreEqual(0, plan.Obstructed); Faces(plan, double3.zero);
                Assert.That(math.length(plan.PositionM.xz), Is.EqualTo(ArrivalRadius).Within(1e-8));
                foreach (var body in content.Bodies)
                    Assert.That(math.length((plan.PositionM - OrbitMath.Position(body, 0)).xz), Is.GreaterThanOrEqualTo(body.RadiusM));
                if (k < 3) Add(content, Fixed(content, BodyRole.Station, (ushort)k, plan.PositionM, 20));
            }
            for (int i = 0; i < content.Bodies.Length; i++)
            {
                var body = content.Bodies[i];
                if (body.Id.Role == BodyRole.Station) { body.State |= BodyStateFlags.Destroyed; content.Bodies[i] = body; }
            }
            Assert.AreEqual(0, Jump(content, p).Adjusted, "Destroyed bodies do not obstruct arrival.");
            var moon = Fixed(content, BodyRole.Moon, 0, double3.zero, 10);
            moon.ParentOrbit = new OrbitElements { Mode = OrbitMode.Fixed, RadiusM = 8000 };
            moon.Orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 1730, PeriodS = 3600 };
            Add(content, moon);
            Assert.AreEqual(1, Jump(content, p, 0).Adjusted);
            Near(new double3(ArrivalRadius, 0, 0), Jump(content, p, 900).PositionM);
            Assert.AreEqual(0, Jump(content, p, 900).Adjusted, "Moon and parent positions are evaluated at the supplied time.");

            var target = new double3(20000, 0, -1000);
            Add(content, Fixed(content, BodyRole.Station, 3, target + new double3(250, 0, 0), 20));
            var cruise = ArrivalSolver.SolveCruise(content, target + new double3(2000, 0, 0), target, 100, 0, p);
            Near(target + Candidate(250, 1, p.StepM), cruise.PositionM); Faces(cruise, target);
            Assert.AreEqual(1, cruise.Adjusted); Assert.AreEqual(0, cruise.Obstructed);
            Assert.That(math.distance(target, cruise.PositionM), Is.EqualTo(250).Within(1e-8));
        }

        [Test] public void FullyBlocked_Flagged()
        {
            using var content = Content(); var p = Parameters;
            var belt = Fixed(content, BodyRole.Belt, 0, double3.zero, 0);
            belt.BeltInnerRadiusM = 9600; belt.BeltOuterRadiusM = 9800; Add(content, belt);
            var plan = Jump(content, p);
            Assert.AreEqual(1, plan.Obstructed); Assert.AreEqual(0, plan.Adjusted);
            Near(new double3(ArrivalRadius, 0, 0), plan.PositionM); Faces(plan, double3.zero);
            Assert.IsTrue(content.TryFind(belt.Id.LocalKey, out int index));
            belt.BeltInnerRadiusM = (float)ArrivalRadius + p.ClearanceM;
            content.Bodies[index] = belt; Assert.AreEqual(0, Jump(content, p).Obstructed, "The inner belt boundary is strict.");
            belt.BeltInnerRadiusM = 9000; belt.BeltOuterRadiusM = (float)ArrivalRadius - p.ClearanceM;
            content.Bodies[index] = belt; Assert.AreEqual(0, Jump(content, p).Obstructed, "The outer belt boundary is strict.");
            belt.BeltOuterRadiusM = 9800; belt.State |= BodyStateFlags.Destroyed;
            content.Bodies[index] = belt; Assert.AreEqual(0, Jump(content, p).Obstructed);

            using var zeroRing = Content(false); zeroRing.LaneRingRadiusM = p.StandoffM;
            Add(zeroRing, Fixed(zeroRing, BodyRole.Star, 0, double3.zero, 10));
            var zero = ArrivalSolver.SolveJump(zeroRing, -1, double2.zero, double2.zero, 0, p);
            Near(double3.zero, zero.PositionM); Assert.IsTrue(float.IsFinite(zero.YawRad));
            Assert.AreEqual(1, zero.Obstructed); Assert.AreEqual(0, zero.Adjusted);
        }

        [Test] public void NoLane_TowardSource()
        {
            using var content = Content(false); var p = Parameters;
            var other = Fixed(content, BodyRole.Station, 0, new double3(20000, 0, 0), 100);
            other.LaneIndex = 99; Add(content, other);
            var target = new double2(10, -7); var source = target + new double2(3, 4);
            var plan = ArrivalSolver.SolveJump(content, -1, target, source, 123, p);
            Near(new double3(6000, 0, 8000), plan.GatePositionM);
            Near(new double3(.6, 0, .8) * (Ring - p.StandoffM), plan.PositionM); Faces(plan, double3.zero);
            Assert.AreEqual(0ul, plan.GateKey); Assert.AreEqual(0, plan.GateRadiusM);
            Assert.AreEqual(0, plan.Adjusted); Assert.AreEqual(0, plan.Obstructed);
            CollectionAssert.AreEqual(Bits(plan), Bits(ArrivalSolver.SolveJump(content, 99, target, source, 123, p)), "A missing gate follows the same fallback; other roles do not match.");
            var same = ArrivalSolver.SolveJump(content, -1, target, target, 123, p);
            Near(new double3(Ring - p.StandoffM, 0, 0), same.PositionM);
        }

        [Test] public void Deterministic()
        {
            using var content = Content(); var p = Parameters;
            Add(content, Fixed(content, BodyRole.Station, 0, new double3(ArrivalRadius, 0, 0), 20));
            var moon = Fixed(content, BodyRole.Moon, 0, double3.zero, 10);
            moon.ParentOrbit = new OrbitElements { Mode = OrbitMode.Elliptic, RadiusM = 3000, Eccentricity = .2, PeriodS = 3600, PhaseRad = .37 };
            moon.Orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 100, PeriodS = 60 };
            Add(content, moon);
            const double time = 1e9 + .123;
            var jump = Bits(Jump(content, p, time));
            var target = new double3(ArrivalRadius, 0, 0);
            var cruise = Bits(ArrivalSolver.SolveCruise(content, double3.zero, target, 20, time, p));
            for (int i = 0; i < 8; i++)
            {
                CollectionAssert.AreEqual(jump, Bits(Jump(content, p, time)));
                CollectionAssert.AreEqual(cruise, Bits(ArrivalSolver.SolveCruise(content, double3.zero, target, 20, time, p)));
            }
            CollectionAssert.AreEqual(Bits(Jump(content, p, 0)), Bits(Jump(content, p, 900)), "Stationary obstruction geometry is independent of time.");
        }

        [Test, Performance, Category("Performance")]
        public void Perf_512Bodies()
        {
            using var content = Content(); var p = Parameters; p.ClearanceM = 1;
            for (ushort i = 0; i < 495; i++)
            {
                var body = Fixed(content, BodyRole.Planet, i, double3.zero, 10);
                body.Orbit = new OrbitElements { Mode = OrbitMode.Circular, RadiusM = 50000 + i * 10, PeriodS = 3600, PhaseRad = i * .1 };
                Add(content, body);
            }
            Add(content, Fixed(content, BodyRole.Station, 0, new double3(ArrivalRadius, 0, 0), 10));
            for (ushort k = 1; k < 16; k++) Add(content, Fixed(content, BodyRole.Station, k, Candidate(ArrivalRadius, k, p.StepM), 10));
            Assert.AreEqual(512, content.Bodies.Length);
            var plan = Jump(content, p, 123); Near(Candidate(ArrivalRadius, 16, p.StepM), plan.PositionM);
            Assert.AreEqual(1, plan.Adjusted); Assert.AreEqual(0, plan.Obstructed);
            for (int i = 0; i < 60; i++) Jump(content, p, 123);
            Stopwatch.GetTimestamp();
            var samples = new double[300]; long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); plan = Jump(content, p, 123);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.AreEqual(0, allocated); Assert.AreEqual(1, plan.Adjusted);
            PerfRun.Gate("ArrivalSolver512Bodies", samples, .1);
        }
    }
}
