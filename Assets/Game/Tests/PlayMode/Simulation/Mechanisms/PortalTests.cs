using System.IO;
using Game.Core;
using Game.Data.Definitions;
using Game.Simulation.Hazards;
using Game.Simulation.Mechanisms;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Mechanisms
{
    /// <summary>HAZ-021: lane-gate portals (ADR-0119 decision 3) on the real gate zone content.</summary>
    [Category("Scenario")]
    public sealed class PortalTests
    {
        static readonly float2 Entry = new float2(0, 60), Exit = new float2(600, 0);
        const float ExitYaw = math.PI / 2;   // the exit faces +X: a ship entering northbound leaves eastbound

        static CombatFixture New() => new CombatFixture(true, typeof(ZoneSystem), typeof(PortalDecideSystem), typeof(PortalTransitSystem));
        static void Log(string line) => File.AppendAllText("Logs/portal-tests.log", line + "\n");

        static Entity Portal(CombatFixture f)
            => HazardSpawn.Portal(f.Em, f.Flight.Catalog.Get<ZoneDefinition>(new ContentId("zone_gate")), Entry, 0, Exit, ExitYaw, 120, 6, 5, 7001, 7002);

        static void Launch(CombatFixture f, Entity e, float speed)
        {
            var v = f.Em.GetComponentData<PhysicsVelocity>(e); v.Linear = new float3(0, 0, speed); v.Angular = 0; f.Em.SetComponentData(e, v);
        }
        static float3 Pos(CombatFixture f, Entity e) => f.Flight.Transform(e).Position;
        static float3 Vel(CombatFixture f, Entity e) => f.Em.GetComponentData<PhysicsVelocity>(e).Linear;

        [Test] public void Transit_KeepsSpeed()
        {
            using var f = New();
            Portal(f);
            var ship = f.Ship("weapon_plasma_turret_small", new float3(0, 0, 0));
            f.Ready(ship); Launch(f, ship, 150);
            float3 before = default; int at = -1;
            for (int i = 0; i < 90 && at < 0; i++)
            {
                before = Vel(f, ship); f.Tick();
                if (Pos(f, ship).x > 300) at = i;
            }
            float3 after = Vel(f, ship), p = Pos(f, ship);
            float3 forward = math.mul(f.Flight.Transform(ship).Rotation, new float3(0, 0, 1));
            Log($"Transit: at tick {at}, pos {p}, speed {math.length(before):F1} -> {math.length(after):F1}, velocity {after}, nose {forward}");
            Assert.GreaterOrEqual(at, 0, "it went through");
            Assert.AreEqual(Exit.x, p.x, 25, "at the exit gate"); Assert.AreEqual(Exit.y, p.z, 25);
            Assert.AreEqual(math.length(before), math.length(after), 3, "speed kept");
            Assert.Greater(after.x, 140, "turned to the exit's forward"); Assert.Less(math.abs(after.z), 10);
            Assert.Greater(forward.x, .95f, "the nose turned with it");
        }

        [Test] public void Jam_Blocks()
        {
            using var f = New();
            var entry = Portal(f);
            var lead = f.Ship("weapon_plasma_turret_small", new float3(-20, 0, 0));
            var second = f.Ship("weapon_plasma_turret_small", new float3(20, 0, -60));
            f.Ready(lead, second); Launch(f, lead, 150); Launch(f, second, 150);
            f.Tick(90);
            float3 a = Pos(f, lead), b = Pos(f, second);
            var link = f.Em.GetComponentData<PortalLink>(entry);
            Log($"Jam: lead at {a}, second at {b}, jammed until {link.JammedUntilTick}");
            Assert.Greater(a.x, 300, "the first ship transits");
            Assert.Less(math.abs(b.x - 20), 15, "the second flies straight through the jammed gate");
            Assert.Greater(b.z, Entry.y + 30);
        }

        [Test] public void Debris_Ignored()
        {
            using var f = New();
            Portal(f);
            var hulk = f.Ship("weapon_plasma_turret_small", new float3(0, 0, 0));
            f.Ready(hulk);
            var g = f.Em.GetComponentData<VoxelGrid>(hulk); g.Kind = GridKind.Hulk; f.Em.SetComponentData(hulk, g);
            f.Em.AddComponent<Game.Voxel.Damage.Hulk>(hulk);
            Launch(f, hulk, 150);
            f.Tick(90);
            float3 p = Pos(f, hulk);
            Log($"Debris: at {p}");
            Assert.Less(math.abs(p.x), 15, "debris never transits");
            Assert.Greater(p.z, Entry.y + 30, "it passed the gate line");
        }
    }
}
