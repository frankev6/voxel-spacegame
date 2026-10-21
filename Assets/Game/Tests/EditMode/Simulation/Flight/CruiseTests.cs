using System;
using Game.Core.Components;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Spatial;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>TRV-015 / ADR-0122: in-world cruise. The real spatial builder, CruiseSystem and CruiseDriveSystem on small grids.</summary>
    public sealed class CruiseTests : GameTestBase
    {
        static readonly CruiseConfig C = CruiseConfig.Default;

        [Test] public void Cap_FollowsClearance()
        {
            Assert.AreEqual(120, CruiseMath.Cap(C, 120, 1000, float.MaxValue), 1e-3);
            Assert.AreEqual(120, CruiseMath.Cap(C, 120, 400, float.MaxValue), 1e-3, "inside the slow radius the cap is flight speed");
            Assert.AreEqual(120 + .3f * 2000, CruiseMath.Cap(C, 120, 3000, float.MaxValue), 1e-2);
            Assert.AreEqual(8000, CruiseMath.Cap(C, 120, 1e7f, float.MaxValue), 1e-3);
            Assert.AreEqual(800, CruiseMath.Cap(C, 120, 1e7f, 800), 1e-3, "a zone lowers the cap");
            float speed = 120;
            for (int i = 0; i < 60; i++) speed = CruiseMath.Step(C, speed, 8000, 1 / 60f);
            Assert.AreEqual(180, speed, .5f, "below 200 m/s speed builds by MinAccel");
            for (int i = 0; i < 600; i++) speed = CruiseMath.Step(C, speed, 8000, 1 / 60f);
            Assert.Greater(speed, 2000, "then by Ramp × speed: about 10 s to 3 km/s");
            Assert.AreEqual(500, CruiseMath.Step(C, 4000, 500, 1 / 60f), 1e-3, "speed drops to the cap at once");
        }

        [Test] public void Spools_ThenCruises_AndHoldsFire()
        {
            using var f = new Fixture();
            var ship = f.Player();
            f.Press(ship); f.Step();
            Assert.AreEqual(CruisePhase.Spooling, f.State(ship).Phase);
            for (int i = 0; i < C.SpoolTicks; i++) { f.Fire(ship, 0); f.Step(); }
            Assert.AreEqual(CruisePhase.On, f.State(ship).Phase);
            float first = f.State(ship).Speed;
            f.Fire(ship, 3); f.Step(30);
            Assert.AreEqual(0u, f.Command(ship).FireGroups, "weapons hold while cruising");
            Assert.Greater(f.State(ship).Speed, first);
            var velocity = f.Em.GetComponentData<PhysicsVelocity>(ship).Linear;
            Assert.AreEqual(f.State(ship).Speed, velocity.z, 1e-2, "the drive flies the ship along its nose");
            Assert.AreEqual(0, velocity.x, 1e-3);
            f.Press(ship); f.Step();
            Assert.AreEqual(CruisePhase.Off, f.State(ship).Phase);
            Assert.AreEqual(CruiseBlock.Cancelled, f.State(ship).Block);
            Assert.LessOrEqual(math.length(f.Em.GetComponentData<PhysicsVelocity>(ship).Linear), 120.01f, "leaving cruise drops to flight speed");
        }

        [Test] public void Firing_CancelsSpool()
        {
            using var f = new Fixture();
            var ship = f.Player();
            f.Press(ship); f.Step();
            f.Fire(ship, 1); f.Step();
            Assert.AreEqual(CruisePhase.Off, f.State(ship).Phase);
            Assert.AreEqual(CruiseBlock.Cancelled, f.State(ship).Block);
        }

        [Test] public void Refused_NearGrid()
        {
            using var f = new Fixture();
            var ship = f.Player(); f.Station(new float3(0, 0, 600));
            f.Press(ship); f.Step();
            Assert.AreEqual(CruisePhase.Off, f.State(ship).Phase);
            Assert.AreEqual(CruiseBlock.TooClose, f.State(ship).Block);
            Assert.Less(f.State(ship).Clearance, C.SlowRadius);
        }

        [Test] public void Refused_MassLocked_NotByNeutrals()
        {
            using var f = new Fixture();
            var ship = f.Player(); var other = f.Ship(new float3(0, 0, 3000), 2);
            f.Press(ship); f.Step();
            Assert.AreEqual(CruisePhase.Spooling, f.State(ship).Phase, "a neutral ship does not lock cruise");
            f.Press(ship); f.Step();
            f.Em.SetComponentData(other, new Allegiance { Faction = 1 });
            f.Press(ship); f.Step();
            Assert.AreEqual(CruisePhase.Off, f.State(ship).Phase);
            Assert.AreEqual(CruiseBlock.MassLocked, f.State(ship).Block);
            Assert.AreEqual(3000, f.State(ship).LockDistance, 10);
        }

        [Test] public void Refused_AfterHit()
        {
            using var f = new Fixture();
            var ship = f.Player();
            f.Step(10);
            f.Em.AddComponentData(ship, new GridDamageStats { LastDamageTick = f.Tick });
            f.Press(ship); f.Step();
            Assert.AreEqual(CruiseBlock.Hit, f.State(ship).Block);
            f.Step(C.DamageLockTicks);
            f.Press(ship); f.Step();
            Assert.AreEqual(CruisePhase.Spooling, f.State(ship).Phase, "the lock lifts after DamageLockTicks");
        }

        [Test] public void Drops_OnHostile()
        {
            using var f = new Fixture();
            var ship = f.Player(); var raider = f.Ship(new float3(0, 0, 20000), 1);
            f.Cruising(ship);
            f.Move(raider, new float3(0, 0, 4000)); f.Step();
            Assert.AreEqual(CruisePhase.Off, f.State(ship).Phase);
            Assert.AreEqual(CruiseBlock.MassLocked, f.State(ship).Block);
            Assert.AreEqual(1, f.State(ship).Exit);
        }

        [Test] public void Arrives_AtSlowRadius()
        {
            using var f = new Fixture();
            var ship = f.Player(); var station = f.Station(new float3(0, 0, 4000));
            f.Cruising(ship);
            float cap = f.State(ship).Cap;
            Assert.AreEqual(120 + .3f * (f.State(ship).Clearance - 1000), cap, 1);
            f.Move(station, new float3(0, 0, 1000 + 86.6f + 1.8f)); f.Step();
            Assert.AreEqual(CruisePhase.Off, f.State(ship).Phase);
            Assert.AreEqual(CruiseBlock.Arrived, f.State(ship).Block);
        }

        [Test] public void Zone_CapsSpeed()
        {
            using var f = new Fixture();
            var ship = f.Player();
            var zone = f.Em.CreateEntity(typeof(CruiseZone), typeof(LocalTransform));
            f.Em.SetComponentData(zone, new CruiseZone { InnerRadius = 0, OuterRadius = 5000, MaxSpeed = 800 });
            f.Em.SetComponentData(zone, LocalTransform.FromPosition(new float3(1000, 0, 0)));
            f.Cruising(ship); f.Step(600);
            Assert.AreEqual(800, f.State(ship).Speed, 1e-2);
            Assert.AreEqual(CruiseMath.ZoneCap(new float2(9000, 0), float2.zero, new CruiseZone { InnerRadius = 8000, OuterRadius = 10000, MaxSpeed = 1 }), 1);
            Assert.AreEqual(float.MaxValue, CruiseMath.ZoneCap(new float2(7000, 0), float2.zero, new CruiseZone { InnerRadius = 8000, OuterRadius = 10000, MaxSpeed = 1 }));
        }

        sealed class Fixture : IDisposable
        {
            readonly World world = new World("CruiseTests");
            readonly SystemHandle builder, cruise, drive;
            readonly FactionRelations factions;
            readonly Entity clock;
            public uint Tick { get; private set; }
            public EntityManager Em => world.EntityManager;
            public Fixture()
            {
                Em.CreateEntity(typeof(GameplayActive)); clock = Em.CreateSingleton(new SimClock());
                factions = FactionRelations.CreateDefault(Allocator.Persistent); Em.CreateSingleton(factions);
                Em.CreateSingleton(FlightConfig.Default);
                builder = world.GetOrCreateSystem<SpatialHashBuildSystem>();
                cruise = world.GetOrCreateSystem<CruiseSystem>();
                drive = world.GetOrCreateSystem<CruiseDriveSystem>();
                Step();
            }
            public Entity Grid(float3 position, byte faction, GridKind kind, int half)
            {
                var e = Em.CreateEntity(typeof(VoxelGrid), typeof(LocalTransform), typeof(Allegiance));
                Em.SetComponentData(e, new VoxelGrid { Kind = kind, CellSize = 1, CellMin = new int3(-half), CellMax = new int3(half - 1) });
                Move(e, position); Em.SetComponentData(e, new Allegiance { Faction = faction }); return e;
            }
            public Entity Ship(float3 position, byte faction) => Grid(position, faction, GridKind.Ship, 1);
            public Entity Station(float3 position) => Grid(position, 2, GridKind.Station, 50);
            public Entity Player()
            {
                var e = Ship(float3.zero, 0);
                Em.AddComponentData(e, default(Cruise)); Em.AddComponentData(e, default(ShipCommand));
                Em.AddComponentData(e, default(PhysicsVelocity)); Em.AddComponentData(e, default(FlightState));
                Em.AddComponentData(e, PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 1000)); return e;
            }
            public void Move(Entity e, float3 position) => Em.SetComponentData(e, LocalTransform.FromPosition(position));
            public void Press(Entity e) { var c = Command(e); c.Edges |= ShipCommandEdges.Cruise; Em.SetComponentData(e, c); }
            public void Fire(Entity e, uint groups) { var c = Command(e); c.FireGroups = groups; Em.SetComponentData(e, c); }
            public ShipCommand Command(Entity e) => Em.GetComponentData<ShipCommand>(e);
            public Cruise State(Entity e) => Em.GetComponentData<Cruise>(e);
            public void Cruising(Entity e)
            {
                Press(e); Step();
                Step(C.SpoolTicks);
                Assert.AreEqual(CruisePhase.On, State(e).Phase, "cruising");
            }
            public void Step(int ticks = 1)
            {
                for (int i = 0; i < ticks; i++)
                {
                    Em.SetComponentData(clock, new SimClock { Tick = ++Tick });
                    builder.Update(world.Unmanaged);
                    cruise.Update(world.Unmanaged); Em.CompleteAllTrackedJobs();
                    drive.Update(world.Unmanaged); Em.CompleteAllTrackedJobs();
                    using (var query = Em.CreateEntityQuery(ComponentType.ReadOnly<SpatialHash>())) query.GetSingleton<SpatialHash>().CompleteAccess();
                    // edges last one tick, as PlayerCommandApplySystem writes them
                    using var ships = Em.CreateEntityQuery(typeof(ShipCommand));
                    foreach (var ship in ships.ToEntityArray(Allocator.Temp))
                    {
                        var c = Em.GetComponentData<ShipCommand>(ship); c.Edges = ShipCommandEdges.None; Em.SetComponentData(ship, c);
                    }
                }
            }
            public void Dispose() { world.Dispose(); factions.Dispose(); }
        }
    }
}
