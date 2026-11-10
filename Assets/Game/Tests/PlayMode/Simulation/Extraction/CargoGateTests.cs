using System.IO;
using Game.Core;
using Game.Data.Definitions;
using Game.Simulation.Extraction;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Extraction
{
    /// <summary>EXT-003 (SPEC-93 decision 4): a lane gate refuses a ship whose towed load outweighs its hold, and takes the load
    /// through with a ship whose hold can carry it.</summary>
    [Category("Scenario")]
    public sealed class CargoGateTests
    {
        static readonly float2 Entry = new float2(0, 60), Exit = new float2(600, 0);
        static CombatFixture New() => new CombatFixture(true, typeof(ZoneSystem), typeof(PortalDecideSystem), typeof(PortalTransitSystem), typeof(TowSystem));
        static void Log(string line) => File.AppendAllText("Logs/cargo-gate-tests.log", line + "\n");
        static float3 Pos(CombatFixture f, Entity e) => f.Flight.Transform(e).Position;

        static void Launch(CombatFixture f, Entity e, float speed)
        {
            var v = f.Em.GetComponentData<PhysicsVelocity>(e); v.Linear = new float3(0, 0, speed); v.Angular = 0; f.Em.SetComponentData(e, v);
        }

        static (Entity ship, Entity load) Setup(CombatFixture f, float capacityT)
        {
            HazardSpawn.Portal(f.Em, f.Flight.Catalog.Get<ZoneDefinition>(new ContentId("zone_gate")), Entry, 0, Exit, math.PI / 2, 120, 6, 5, 7101, 7102);
            var ship = f.Ship("weapon_plasma_turret_small", new float3(0, 0, 0));
            var load = f.Ship("weapon_plasma_turret_small", new float3(0, 0, -24));
            f.Ready(ship, load);
            var g = f.Em.GetComponentData<VoxelGrid>(load); g.Kind = GridKind.Device; f.Em.SetComponentData(load, g);   // a loose crate, not a ship
            f.Em.AddComponentData(ship, new CargoHold { CapacityT = capacityT });
            Assert.That(TowApi.Attach(f.Em, ship, load, 26, 1), Is.True);
            Launch(f, ship, 80); Launch(f, load, 80);
            return (ship, load);
        }

        [Test] public void TooHeavy_Refused()
        {
            using var f = New();
            var (ship, load) = Setup(f, 0);
            float massT = f.Em.GetComponentData<TowLoad>(load).MassT;
            f.Tick(120);
            float3 p = Pos(f, ship);
            Log($"Refused: load {massT:F1} t, ship at {p}, refused {f.Em.HasComponent<CargoRefused>(ship)}");
            Assert.That(massT, Is.GreaterThan(0));
            Assert.That(f.Em.HasComponent<CargoRefused>(ship), Is.True, "the gate says why");
            Assert.That(math.abs(p.x), Is.LessThan(30), "no transit: it flew on past the gate line");
            Assert.That(p.z, Is.GreaterThan(Entry.y + 30));
        }

        [Test] public void HoldTakesIt_LoadGoesThrough()
        {
            using var f = New();
            var (ship, load) = Setup(f, 10000);
            f.Tick(120);
            float3 p = Pos(f, ship), q = Pos(f, load);
            Log($"Passed: ship at {p}, load at {q}, gap {math.distance(p, q):F1}, still towing {TowApi.IsTowing(f.Em, ship)}");
            Assert.That(p.x, Is.GreaterThan(300), "the ship transits");
            Assert.That(q.x, Is.GreaterThan(300), "the load came with it");
            Assert.That(math.distance(p, q), Is.LessThan(60), "still on the cable");
            Assert.That(TowApi.IsTowing(f.Em, ship), Is.True);
        }
    }
}
