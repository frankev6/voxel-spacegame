using System.IO;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation.Hazards;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Hazards
{
    /// <summary>HAZ-016: reactor pulses with shadows (ADR-0119 decision 4) on real zone content.</summary>
    [Category("Scenario")]
    public sealed class PulseTests
    {
        static CombatFixture New() => new CombatFixture(true, typeof(PulseSystem));
        static void Log(string line) => File.AppendAllText("Logs/pulse-tests.log", line + "\n");

        /// <summary>A reactor at the origin pulsing every 2 s from tick 120, a test ship 40 m from it, and (optionally) a wreck slab between.</summary>
        static (Entity ship, Entity zone) Setup(CombatFixture f, bool wreck, bool shield = false)
        {
            var def = f.Flight.Catalog.Get<ZoneDefinition>(new ContentId("zone_pulse_reactor"));
            var zone = HazardSpawn.Zone(f.Em, def, float2.zero, 0, 0, 77);
            HazardSpawn.Schedule(f.Em, zone, 2, 1f / 60);
            var ship = f.Ship("weapon_plasma_turret_small", new float3(0, 0, 40), shield);
            if (wreck)
            {
                var source = new VoxelStructure(.5f);
                for (int x = -14; x <= 14; x++) for (int y = -4; y <= 4; y++)
                    Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = f.Flight.Hull, Origin = new int3(x, y, 0) }, f.Flight.Content));
                var slab = f.Flight.SpawnSource(source, new float3(0, 0, 20), motion: GridMotion.Static);
                f.Ready(ship);
                for (int i = 0; i < 90 && !(f.Em.HasComponent<GridColliderState>(slab) && f.Em.GetComponentData<GridColliderState>(slab).Phase == GridColliderPhase.Ready); i++) f.Tick();
                Assert.AreEqual(GridColliderPhase.Ready, f.Em.GetComponentData<GridColliderState>(slab).Phase, "the wreck has its collider");
            }
            else f.Ready(ship);
            return (ship, zone);
        }
        static float Taken(CombatFixture f, Entity e) => f.Em.HasComponent<GridDamageStats>(e) ? f.Em.GetComponentData<GridDamageStats>(e).DamageTaken : 0;

        /// <summary>Ticks to just past the third pulse after now.</summary>
        static void ThreePulses(CombatFixture f)
        {
            uint t = f.World.GetSingleton<SimClock>().Tick;
            uint end = (t / 120 + 3) * 120 + 5;
            while (f.World.GetSingleton<SimClock>().Tick < end) f.Tick();
        }

        [Test] public void Open_TakesPulse()
        {
            using var f = New();
            var (ship, _) = Setup(f, false);
            float before = Taken(f, ship); int cells = f.Cells(ship);
            ThreePulses(f);
            Log($"Open: damage {before:F0} -> {Taken(f, ship):F0}, cells {cells} -> {f.Cells(ship)}");
            Assert.Greater(Taken(f, ship) - before, 500, "three pulses reach a ship in the open");
        }

        [Test] public void Shadowed_Safe()
        {
            using var f = New();
            var (ship, _) = Setup(f, true);
            float before = Taken(f, ship);
            ThreePulses(f);
            Log($"Shadowed: damage {before:F0} -> {Taken(f, ship):F0}");
            Assert.AreEqual(before, Taken(f, ship), 1e-3f, "a ship behind the wreck is safe");
        }

        [Test] public void Shield_DrainsFirst()
        {
            using var f = New();
            var (ship, _) = Setup(f, false, shield: true);
            f.Tick(30);
            var shield = f.Em.GetComponentData<ShieldState>(ship);
            Assert.AreEqual(1, shield.Active, "the shield is up");
            float hp = shield.Hp, before = Taken(f, ship);
            uint t = f.World.GetSingleton<SimClock>().Tick, next = (t / 120 + 1) * 120 + 3;
            while (f.World.GetSingleton<SimClock>().Tick < next) f.Tick();
            var after = f.Em.GetComponentData<ShieldState>(ship);
            Log($"Shield: hp {hp:F0}/{shield.MaxHp:F0} -> {after.Hp:F0}, hull damage {before:F0} -> {Taken(f, ship):F0}");
            Assert.Less(after.Hp, hp - 100, "the pulse drains the shield");
            if (hp > 500) Assert.AreEqual(before, Taken(f, ship), 1e-3f, "a shield with HP to spare keeps the hull whole");
        }

        [Test] public void Deterministic()
        {
            float[] taken = new float[2]; int[] cells = new int[2];
            for (int run = 0; run < 2; run++)
            {
                using var f = New();
                var (ship, _) = Setup(f, false);
                ThreePulses(f);
                taken[run] = Taken(f, ship); cells[run] = f.Cells(ship);
            }
            Log($"Deterministic: {taken[0]} / {taken[1]}, cells {cells[0]} / {cells[1]}");
            Assert.AreEqual(taken[0], taken[1]); Assert.AreEqual(cells[0], cells[1]);
        }
    }
}
