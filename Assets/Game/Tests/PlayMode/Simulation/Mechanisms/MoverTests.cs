using Game.Core;
using Game.Core.Components;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Mechanisms
{
    /// <summary>HAZ-013: moving structures (ADR-0119 decision 1) are kinematic grids that land on their path pose every tick.</summary>
    [Category("Scenario")]
    public sealed class MoverTests
    {
        static CombatFixture New() => new CombatFixture(false, typeof(MoverSystem));

        // A hull bar along +x from the root: 15 m long, 1.5 m tall, 1 m deep.
        static Entity Bar(CombatFixture f, float3 at, int length = 30, int depth = 2)
        {
            var source = new VoxelStructure(.5f);
            for (int x = 0; x < length; x++) for (int y = -1; y <= 1; y++) for (int z = 0; z < depth; z++)
                Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = f.Flight.Hull, Origin = new int3(x, y, z) }, f.Flight.Content));
            return f.Flight.SpawnSource(source, at, motion: GridMotion.Kinematic);
        }

        static void WaitBody(CombatFixture f, Entity root)
        {
            for (int i = 0; i < 60; i++)
            {
                f.Tick();
                if (f.Em.HasComponent<PhysicsMass>(root) && f.Em.HasComponent<GridColliderState>(root) &&
                    f.Em.GetComponentData<GridColliderState>(root).Phase == GridColliderPhase.Ready) return;
            }
            Assert.Fail("mover body not ready");
        }

        static uint Tick(CombatFixture f) => f.World.GetSingleton<SimClock>().Tick;

        static void Expected(CombatFixture f, Entity root, out float3 position, out float yaw)
        {
            var m = f.Em.GetComponentData<Mover>(root);
            using var points = f.Em.GetBuffer<MoverPoint>(root).ToNativeArray(Allocator.Temp);
            MoverMath.Pose(m, points, Tick(f), out position, out yaw);
        }

        static float Yaw(CombatFixture f, Entity root) => Planar.YawFromForward(math.mul(f.Flight.Transform(root).Rotation, new float3(0, 0, 1)));

        /// <summary>A rebase right after a mover is authored, while its grid is still spawning (no physics body yet), shifts its path too:
        /// Track 01's rig and door, spawned as the race opened, were left 2.3 km off where they belong.</summary>
        [Test] public void Rebase_WhileSpawning_ShiftsPath()
        {
            using var f = New();
            Entity record;
            using (var q = f.Em.CreateEntityQuery(typeof(OriginShiftRecord)))
                record = q.IsEmpty ? f.Em.CreateEntity(typeof(OriginShiftRecord)) : q.GetSingletonEntity();
            f.Tick();   // the world runs with no mover yet
            // a root still spawning (its pose, no physics body yet), authored just before a rebase, as a race opening does
            var arm = f.Em.CreateEntity(typeof(LocalTransform));
            f.Em.SetComponentData(arm, LocalTransform.Identity);
            HazardSpawn.AddMover(f.Em, arm, new Mover { Path = MoverPath.Rotate, Origin = new float3(0, 0, 0), Rate = .5f, StartTick = Tick(f), Playing = 1 });
            var r = f.Em.GetComponentData<OriginShiftRecord>(record);
            f.Em.SetComponentData(record, new OriginShiftRecord { Sequence = r.Sequence + 1, Delta = new float3(100, 0, -50) });
            f.Tick();
            var origin = f.Em.GetComponentData<Mover>(arm).Origin;
            TestContext.WriteLine($"Rebase while spawning: mover origin {origin}");
            Assert.AreEqual(100, origin.x, 1e-3f); Assert.AreEqual(-50, origin.z, 1e-3f);
        }

        [Test] public void Rotate_TickExact()
        {
            using var f = New();
            var arm = Bar(f, new float3(0, 0, 0)); WaitBody(f, arm);
            HazardSpawn.AddMover(f.Em, arm, new Mover { Path = MoverPath.Rotate, Origin = f.Flight.Transform(arm).Position, Rate = .5f, StartTick = Tick(f), Playing = 1 });
            float worst = 0, worstPos = 0;
            for (int minute = 0; minute < 10; minute++)
            {
                f.Tick(3600);
                Expected(f, arm, out var p, out var yaw);
                worst = math.max(worst, math.abs(MoverMath.Wrap(Yaw(f, arm) - yaw)));
                worstPos = math.max(worstPos, math.distance(f.Flight.Transform(arm).Position, p));
            }
            TestContext.WriteLine($"Rotate over 10 minutes: worst yaw error {worst:G4} rad, position {worstPos:G4} m");
            Assert.Less(worst, 1e-3f, "the arm keeps its rate with no drift over 10 minutes");
            Assert.Less(worstPos, 1e-3f, "the hub stays put");
        }

        [Test] public void Rail_PingPong()
        {
            var points = new NativeArray<MoverPoint>(new[] { new MoverPoint { Pose = 0 }, new MoverPoint { Pose = new float3(10, 0, 0) } }, Allocator.Temp);
            var m = new Mover { Path = MoverPath.Rail, Speed = 5, Playing = 1, PingPong = 1 };
            float X(double seconds) { MoverMath.Pose(m, points, (uint)math.round(seconds * 60), out var p, out _); return p.x; }
            Assert.AreEqual(5, X(1), 1e-3f); Assert.AreEqual(10, X(2), 1e-3f); Assert.AreEqual(5, X(3), 1e-3f); Assert.AreEqual(0, X(4), 1e-3f);
            Assert.AreEqual(2.5f, X(4.5), 1e-3f);
            m.Reverse = 1;   // runs the points backwards: from 0 it bounces off the start
            Assert.AreEqual(5, X(1), 1e-3f); Assert.AreEqual(10, X(2), 1e-3f);
            m.Reverse = 0; m.PingPong = 0;   // a closed loop returns along the closing segment
            Assert.AreEqual(10, X(2), 1e-3f); Assert.AreEqual(5, X(3), 1e-3f); Assert.AreEqual(0, X(4), 1e-3f); Assert.AreEqual(5, X(5), 1e-3f);
            points.Dispose();
        }

        [Test] public void Toggle_Duration()
        {
            using var f = New();
            var door = Bar(f, new float3(30, 0, 0), 12, 1); WaitBody(f, door);
            var shut = f.Flight.Transform(door).Position; var open = shut + new float3(0, 0, 4);
            HazardSpawn.AddMover(f.Em, door, new Mover { Path = MoverPath.Toggle, Rate = .5f, StartTick = Tick(f), Playing = 1 },
                new[] { new MoverPoint { Pose = shut }, new MoverPoint { Pose = open } });
            f.Tick(60);
            Assert.AreEqual(2, math.distance(f.Flight.Transform(door).Position, shut), .01f, "halfway after 1 s of its 2 s");
            f.Tick(60);
            Assert.Less(math.distance(f.Flight.Transform(door).Position, open), .01f, "open after its duration");
            f.Tick(60);
            Assert.Less(math.distance(f.Flight.Transform(door).Position, open), .01f, "and it stays open");
            Assert.Less(math.length(f.Flight.Velocity(door).Linear), 1e-3f);
            var m = f.Em.GetComponentData<Mover>(door); m.Reverse = 1; m.StartTick = Tick(f); f.Em.SetComponentData(door, m);
            f.Tick(120);
            Assert.Less(math.distance(f.Flight.Transform(door).Position, shut), .01f, "shut again after its duration");
        }

        struct Outcome { public float3 Position, Velocity; public quaternion Rotation; public int Cells, CellsBefore; }

        static Outcome Bat()
        {
            using var f = New();
            var arm = Bar(f, new float3(0, 0, 0));
            // The starter WRAITH: a light synthetic hull is shoved over several ticks, each below the ram-energy floor.
            // TODO(batch-5): re-measure on WRAITH. Its bow reaches 11.5 m ahead of its origin (the retired 24.6 t Fighter's 7.5 m),
            // so it sits at z = -13 to spawn clear of the arm; the batted speed and ram damage were measured on the Fighter.
            var ship = f.Flight.SpawnBlueprint(f.Flight.Catalog.Get<Game.Data.Definitions.ShipBlueprintDefinition>(new ContentId("ship_nocturne_wraith")), new float3(0, 0, -13));
            WaitBody(f, arm); f.Ready(ship);
            HazardSpawn.AddMover(f.Em, arm, new Mover { Path = MoverPath.Rotate, Origin = f.Flight.Transform(arm).Position, Rate = 1.5f, StartTick = Tick(f), Playing = 1 });
            int before = f.Cells(ship);
            f.Tick(180);
            return new Outcome { Position = f.Flight.Transform(ship).Position, Rotation = f.Flight.Transform(ship).Rotation,
                Velocity = f.Flight.Velocity(ship).Linear, Cells = f.Cells(ship), CellsBefore = before };
        }

        [Test] public void BatsShip()
        {
            var o = Bat();
            TestContext.WriteLine($"Batted ship: at {o.Position}, moving {o.Velocity} ({math.length(o.Velocity.xz):F2} m/s), cells {o.CellsBefore} -> {o.Cells}");
            Assert.Greater(math.length(o.Velocity.xz), 2, "the sweeping arm bats the ship aside");
            Assert.Greater(math.distance(o.Position, new float3(0, 0, -13)), 1);
            Assert.Less(o.Cells, o.CellsBefore, "the hit raises ram damage");
        }

        [Test] public void Deterministic()
        {
            var a = Bat(); var b = Bat();
            Assert.AreEqual(a.Position, b.Position); Assert.AreEqual(a.Velocity, b.Velocity);
            Assert.AreEqual(a.Rotation.value, b.Rotation.value); Assert.AreEqual(a.Cells, b.Cells);
        }
    }
}
