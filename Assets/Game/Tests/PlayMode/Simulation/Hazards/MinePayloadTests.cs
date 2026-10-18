using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation.Flight;
using Game.Simulation.Hazards;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Hazards
{
    /// <summary>HAZ-018: the mine family (ADR-0119 decision 6) on real mine content and the starter WRAITH.</summary>
    [Category("Scenario")]
    public sealed class MinePayloadTests
    {
        static CombatFixture New() => new CombatFixture(false, typeof(MineSystem), typeof(MineAnchorSystem), typeof(StatusSystem), typeof(ZoneSystem));
        static T Def<T>(CombatFixture f, string id) where T : ContentDefinition => f.Flight.Catalog.Get<T>(new ContentId(id));
        static uint Tick(CombatFixture f) => f.World.GetSingleton<SimClock>().Tick;

        static (Entity ship, Entity mine) Setup(CombatFixture f, string mineId, float2 mineAt, float3 shipAt = default)
        {
            // TODO(batch-5): re-measure on WRAITH. Mine distances and push floors were set on the retired 15 m Fighter; the WRAITH's
            // bow reaches 11.5 m ahead of its origin, so a mine 16 m ahead sits closer to the hull.
            var ship = f.Flight.SpawnBlueprint(Def<ShipBlueprintDefinition>(f, "ship_nocturne_wraith"), shipAt);
            var mine = HazardSpawn.Mine(f.Em, Def<MineDefinition>(f, mineId), mineAt, 0, 0, 900, 0);
            f.Ready(ship);
            return (ship, mine);
        }

        /// <summary>Ticks until the mine has gone off (its root is gone or spent); returns the tick it did. <paramref name="push"/> sums the
        /// ship's external push over the detonation tick (a blast pushes on the tick it goes off).</summary>
        static uint RunToDetonation(CombatFixture f, Entity mine, Entity ship, out float3 push, int limit = 900)
        {
            push = 0;
            for (int i = 0; i < limit; i++)
            {
                uint seen = f.Em.GetComponentData<ExternalPush>(ship).Tick;
                f.Tick();
                var p = f.Em.GetComponentData<ExternalPush>(ship);
                if (!f.Em.Exists(mine) || f.Em.HasComponent<MineState>(mine) && f.Em.GetComponentData<MineState>(mine).Phase == MinePhase.Spent)
                { if (p.Tick != seen) push = p.LinearDv; return Tick(f); }
            }
            Assert.Fail("the mine never went off"); return 0;
        }

        static float3 SumPush(CombatFixture f, Entity ship, int ticks)
        {
            float3 sum = 0; uint seen = f.Em.GetComponentData<ExternalPush>(ship).Tick;
            for (int i = 0; i < ticks; i++)
            {
                f.Tick(); var p = f.Em.GetComponentData<ExternalPush>(ship);
                if (p.Tick != seen) { sum += p.LinearDv; seen = p.Tick; }
            }
            return sum;
        }

        [Test] public void Repulse_NoDamage()
        {
            using var f = New();
            var (ship, mine) = Setup(f, "mine_repulsor", new float2(0, 16));
            int cells = f.Cells(ship);
            RunToDetonation(f, mine, ship, out var blast);
            var push = blast + SumPush(f, ship, 3);
            TestContext.WriteLine($"Repulsor push {push}");
            Assert.Less(push.z, -1, "thrown away from the mine");
            Assert.AreEqual(cells, f.Cells(ship), "no damage");
        }

        [Test] public void Emp_InhibitsThrusters()
        {
            using var f = New();
            var (ship, mine) = Setup(f, "mine_emp", new float2(0, 16));
            int cells = f.Cells(ship);
            uint at = RunToDetonation(f, mine, ship, out _);
            f.Tick(2);
            var thrusters = f.Em.GetBuffer<FlightThruster>(ship).ToNativeArray(Allocator.Temp);
            Assert.Greater(thrusters.Length, 0);
            uint until = 0; float forward = 0;
            foreach (var t in thrusters)
            {
                Assert.GreaterOrEqual(f.Em.GetComponentData<Inhibited>(t.Thruster).UntilTick, at + 170, "every thruster is off for 3 s");
                until = math.max(until, f.Em.GetComponentData<Inhibited>(t.Thruster).UntilTick);
                if (t.ForceDirLocal.z > .5f) forward += t.MaxForce;
            }
            thrusters.Dispose();
            // Full thrust is commanded through the blackout: the ship only drifts (carry keeps what it had), then answers again.
            float before = math.length(f.Flight.Velocity(ship).Linear.xz);
            for (int i = 0; i < 600 && Tick(f) < until; i++) { f.Flight.Command(ship, thrust: new float2(0, 1)); f.Tick(); }
            float drifting = math.length(f.Flight.Velocity(ship).Linear.xz);
            for (int i = 0; i < 60; i++) { f.Flight.Command(ship, thrust: new float2(0, 1)); f.Tick(); }
            float after = math.length(f.Flight.Velocity(ship).Linear.xz);
            TestContext.WriteLine($"EMP: {before:F2} m/s at the blast, {drifting:F2} at the end of the blackout, {after:F2} once it ends");
            Assert.Less(drifting, before + 1, "no thrust while inhibited");
            // One second of full thrust after the blackout (0.25 s spool-up) recovers most of forward thrust over mass.
            Assert.Greater(after - drifting, .6f * forward * f.Flight.Mass(ship).InverseMass, "thrust returns after its seconds");
            Assert.AreEqual(cells, f.Cells(ship), "no damage");
        }

        [Test] public void Gravity_TemporaryWell()
        {
            using var f = New();
            var (ship, mine) = Setup(f, "mine_gravity", new float2(0, 16));
            RunToDetonation(f, mine, ship, out _);
            using (var q = f.Em.CreateEntityQuery(typeof(MineWell))) Assert.AreEqual(1, q.CalculateEntityCount(), "a well where it went off");
            var pull = SumPush(f, ship, 10);
            Assert.Greater(pull.z, .5f, "the ship is pulled toward the well");
            f.Tick(360);   // 6 s after the blast, counting the 10 ticks above
            using (var q = f.Em.CreateEntityQuery(typeof(MineWell))) Assert.AreEqual(0, q.CalculateEntityCount(), "gone after its 6 s");
        }

        static bool SeekerTriggers(float shipSpeed)
        {
            using var f = New();
            // The seeker waits 60 m behind a ship that flies away at full thrust from shipSpeed.
            var (ship, mine) = Setup(f, "mine_seeker", new float2(0, -60));
            var v = f.Flight.Velocity(ship); v.Linear = new float3(0, 0, shipSpeed); f.Em.SetComponentData(ship, v);
            for (int i = 0; i < 600; i++)
            {
                if (shipSpeed > 0) f.Flight.Command(ship, thrust: new float2(0, 1));
                f.Tick();
                if (!f.Em.Exists(mine) || f.Em.GetComponentData<MineState>(mine).Phase >= MinePhase.Triggered) return true;
            }
            return false;
        }

        [Test] public void Seeker_ChasesSlowShip()
        {
            Assert.IsTrue(SeekerTriggers(0), "a seeker reaches a ship that sits still");
            Assert.IsFalse(SeekerTriggers(40), "a ship flying away at speed outruns it");
        }
    }
}
