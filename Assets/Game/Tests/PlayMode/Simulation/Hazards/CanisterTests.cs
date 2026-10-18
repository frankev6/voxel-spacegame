using System.IO;
using Game.Core;
using Game.Data.Definitions;
using Game.Simulation.Hazards;
using Game.Simulation.Power;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using CombatFixture = Game.Tests.PlayMode.Simulation.Weapons.CombatScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Simulation.Hazards
{
    /// <summary>HAZ-019: energy canisters (ADR-0119 decision 7) on real canister content and the starter WRAITH.</summary>
    // TODO(batch-5): re-measure on WRAITH (power capacity >= 70, recharge and fly-through reach were checked on the retired Fighter).
    [Category("Scenario")]
    public sealed class CanisterTests
    {
        static CombatFixture New() => new CombatFixture(true, typeof(PickupSystem), typeof(StatusSystem));
        static T Def<T>(CombatFixture f, string id) where T : ContentDefinition => f.Flight.Catalog.Get<T>(new ContentId(id));
        static void Log(string line) => File.AppendAllText("Logs/canister-tests.log", line + "\n");

        static Entity Canister(CombatFixture f, float2 at)
        {
            var e = HazardSpawn.Canister(f.Em, Def<ShipBlueprintDefinition>(f, "ship_device_canister"), at, 0, 3, 901);
            for (int i = 0; i < 60 && !f.Em.HasComponent<Unity.Physics.PhysicsMass>(e); i++) f.Tick();
            Assert.IsTrue(f.Em.HasComponent<Unity.Physics.PhysicsMass>(e), "the canister spawned");
            return e;
        }

        [Test] public void Pickup_GrantsOnce()
        {
            using var f = New();
            var ship = f.Flight.SpawnBlueprint(Def<ShipBlueprintDefinition>(f, "ship_nocturne_wraith"), default);
            f.Ready(ship);
            var canister = Canister(f, new float2(0, 60));
            var power = f.Em.GetComponentData<ShipPower>(ship);
            Assert.GreaterOrEqual(power.Capacity, 70, "room for the grant");
            int grants = 0; float jump = 0;
            for (int i = 0; i < 600; i++)
            {
                // The starter recharges fast: hold it 70 below full until the canister is collected, so the grant shows.
                if (f.Em.Exists(canister) && f.Em.HasComponent<Canister>(canister))
                { var p = f.Em.GetComponentData<ShipPower>(ship); p.Stored = p.Capacity - 70; f.Em.SetComponentData(ship, p); }
                float before = f.Em.GetComponentData<ShipPower>(ship).Stored;
                f.Flight.Command(ship, thrust: new float2(0, 1)); f.Tick();
                float delta = f.Em.GetComponentData<ShipPower>(ship).Stored - before;
                if (delta > 30) { grants++; jump = delta; }
            }
            Log($"Pickup: capacity {power.Capacity:F0}, {grants} grant(s), jump {jump:F2}, ship at z {f.Flight.Transform(ship).Position.z:F0}");
            Assert.AreEqual(1, grants, "one grant");
            Assert.AreEqual(60, jump, 4, "by the canister's energy");
            Assert.IsFalse(f.Em.Exists(canister) && f.Em.HasComponent<Canister>(canister), "the canister is spent");
        }

        /// <summary>A racer flies through at race speed: the reach meets its bounding sphere before the hulls touch, so it is unhurt.</summary>
        [Test] public void FlyThrough_AtSpeed_Unhurt()
        {
            using var f = New();
            var ship = f.Flight.SpawnBlueprint(Def<ShipBlueprintDefinition>(f, "ship_nocturne_wraith"), default);
            f.Ready(ship);
            var canister = Canister(f, new float2(0, 150));
            int cells = f.Cells(ship);
            var v = f.Em.GetComponentData<Unity.Physics.PhysicsVelocity>(ship); v.Linear = new float3(0, 0, 100); f.Em.SetComponentData(ship, v);
            int spentAt = -1;
            for (int i = 0; i < 180; i++)
            {
                f.Flight.Command(ship, thrust: new float2(0, 1)); f.Tick();
                if (spentAt < 0 && !(f.Em.Exists(canister) && f.Em.HasComponent<Canister>(canister))) spentAt = i;
            }
            float z = f.Flight.Transform(ship).Position.z;
            Log($"FlyThrough: spent at tick {spentAt}, ship at z {z:F0}, cells {cells} -> {f.Cells(ship)}, canister exists {f.Em.Exists(canister)}");
            Assert.GreaterOrEqual(spentAt, 0, "collected");
            Assert.Greater(z, 200, "flew on past it");
            Assert.AreEqual(cells, f.Cells(ship), "no cell lost");
        }

        [Test] public void Shot_Explodes()
        {
            using var f = New();
            var canister = Canister(f, new float2(0, 0));
            // A hull slab 3 m to the side, out of the line of fire: the canister's 6 m blast reaches it.
            var source = new VoxelStructure(.5f);
            for (int z = -4; z <= 4; z++) for (int y = -2; y <= 2; y++)
                Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = f.Flight.Hull, Origin = new int3(0, y, z) }, f.Flight.Content));
            var slab = f.Flight.SpawnSource(source, new float3(3.5f, 0, 0), motion: GridMotion.Static);
            var shooter = f.Ship("weapon_plasma_turret_small", new float3(0.25f, 0, -25));
            f.Ready(shooter); f.Tick(30);
            int slabCells = f.Cells(slab);
            float stored = f.Em.GetComponentData<ShipPower>(shooter).Stored;
            float3 aim = f.Flight.Transform(canister).TransformPoint(f.Em.GetComponentData<Unity.Physics.PhysicsMass>(canister).CenterOfMass);
            int brokeAt = -1;
            for (int tick = 0; tick < 300 && brokeAt < 0; tick++)
            {
                f.Fire(shooter, aim); f.Tick();
                if (!f.Em.HasComponent<Canister>(canister)) brokeAt = tick;
            }
            f.Stop(shooter); f.Tick(10);
            Log($"Shot: broke at tick {brokeAt}, slab {slabCells} -> {f.Cells(slab)} cells, shooter stored {stored:F1} -> {f.Em.GetComponentData<ShipPower>(shooter).Stored:F1}");
            Assert.GreaterOrEqual(brokeAt, 0, "the shots break it");
            Assert.Less(f.Cells(slab), slabCells, "its charge goes off and hurts the slab beside it");
        }
    }
}
