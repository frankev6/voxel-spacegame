using Game.Simulation.Commands;
using Game.Tests.Common;
using Game.Voxel.Damage;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Flight
{
    /// <summary>FLT-005: edge latching, missing/dead players, Cruise ownership and finite commands (SPEC-20 §5.1).</summary>
    public sealed class PlayerCommandApplyTests : GameTestBase
    {
        EcsTestWorld world;
        Entity input;
        EntityManager EM => world.EntityManager;
        [SetUp]
        public void SetUp()
        {
            world = CreateWorld(typeof(PlayerCommandApplySystem));
            world.AddGameplayActive(); input = world.AddSingleton(default(PlayerCommand));
        }
        Entity Ship() => EM.CreateEntity(typeof(PlayerShip), typeof(ShipCommand));
        void Set(PlayerCommand value) => EM.SetComponentData(input, value);
        ShipCommand Tick(Entity ship) { world.StepTicks(1); return EM.GetComponentData<ShipCommand>(ship); }
        static PlayerEdgeCounters Counters(byte value) => new PlayerEdgeCounters
        {
            Interact = value, Ability1 = value, Ability2 = value, Ability3 = value, Ability4 = value,
            TargetNext = value, TargetUnderCursor = value, TargetClear = value, AutofireToggle = value
        };
        const ShipCommandEdges AllEdges = (ShipCommandEdges)511;

        [Test]
        public void Cruise_NotAShipEdge()
        {
            var ship = Ship();
            Set(new PlayerCommand { Edges = new PlayerEdgeCounters { Cruise = 3 } });
            Assert.AreEqual(ShipCommandEdges.None, Tick(ship).Edges);
            Assert.AreEqual(0, world.GetSingleton<PlayerEdgeConsumed>().Value.Cruise);
            EM.AddComponent<Hulk>(ship); world.StepTicks(1);
            Assert.AreEqual(0, world.GetSingleton<PlayerEdgeConsumed>().Value.Cruise);
            EM.DestroyEntity(ship); world.StepTicks(1);
            Assert.AreEqual(0, world.GetSingleton<PlayerEdgeConsumed>().Value.Cruise);
        }

        [Test]
        public void Edge_OncePerCounterStep()
        {
            var first = Ship(); var second = Ship();
            Set(new PlayerCommand { Edges = Counters(2) });
            for (int tick = 0; tick < 3; tick++)
            {
                var expected = tick < 2 ? AllEdges : ShipCommandEdges.None;
                Assert.AreEqual(expected, Tick(first).Edges);
                Assert.AreEqual(expected, EM.GetComponentData<ShipCommand>(second).Edges);
            }
            using (var query = EM.CreateEntityQuery(typeof(PlayerEdgeConsumed)))
                query.SetSingleton(new PlayerEdgeConsumed { Value = Counters(255) });
            Set(new PlayerCommand { Edges = Counters(0) });
            Assert.AreEqual(AllEdges, Tick(first).Edges, "counter wrap consumes one press");
            Assert.AreEqual(ShipCommandEdges.None, Tick(first).Edges);
        }

        [Test]
        public void Edge_ClearedNextTick()
        {
            var ship = Ship(); var ai = EM.CreateEntity(typeof(ShipCommand));
            var player = new PlayerCommand { Thrust = new float2(.25f, -.75f), Turn = .5f, AimWorld = new float3(1, 2, 3),
                AimValid = 1, Flags = ShipCommandFlags.Boost, FireGroups = 17, Edges = Counters(1) };
            Set(player); Assert.AreEqual(AllEdges, Tick(ship).Edges);
            player.Thrust = new float2(-.5f, 1); player.Turn = -.25f; Set(player);
            var command = Tick(ship);
            Assert.AreEqual(ShipCommandEdges.None, command.Edges); Assert.AreEqual(player.Thrust, command.Thrust);
            Assert.AreEqual(player.Turn, command.Turn); Assert.AreEqual(player.AimWorld, command.AimWorld);
            Assert.AreEqual(1, command.AimValid); Assert.AreEqual(player.Flags, command.Flags); Assert.AreEqual(17, command.FireGroups);
            Assert.AreEqual(default(ShipCommand), EM.GetComponentData<ShipCommand>(ai), "AI commands have a separate writer");
        }

        [Test]
        public void NoShip_DropsEdges()
        {
            Set(new PlayerCommand { Edges = Counters(5) }); world.StepTicks(1);
            Assert.AreEqual(Counters(5), world.GetSingleton<PlayerEdgeConsumed>().Value);
            var ship = Ship(); Assert.AreEqual(ShipCommandEdges.None, Tick(ship).Edges);
        }

        [Test]
        public void Hulk_DropsEdges()
        {
            var ship = Ship(); EM.AddComponent<Hulk>(ship);
            var untouched = new ShipCommand { Turn = .75f, Edges = ShipCommandEdges.TargetClear };
            EM.SetComponentData(ship, untouched); Set(new PlayerCommand { Edges = Counters(3) });
            Assert.AreEqual(untouched, Tick(ship)); Assert.AreEqual(Counters(3), world.GetSingleton<PlayerEdgeConsumed>().Value);
            EM.RemoveComponent<Hulk>(ship);
            Assert.AreEqual(ShipCommandEdges.None, Tick(ship).Edges);
        }

        [Test]
        public void NonFinite_Sanitised()
        {
            var ship = Ship();
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                Set(new PlayerCommand { Thrust = new float2(bad, -3), Turn = bad, AimWorld = new float3(2, bad, 3), AimValid = 1 });
                var command = Tick(ship);
                Assert.AreEqual(new float2(0, -1), command.Thrust); Assert.AreEqual(0, command.Turn);
                Assert.AreEqual(new float3(2, 0, 3), command.AimWorld); Assert.AreEqual(0, command.AimValid);
                Set(new PlayerCommand { Thrust = new float2(3, bad), Turn = 2, AimValid = 1 });
                command = Tick(ship); Assert.AreEqual(new float2(1, 0), command.Thrust); Assert.AreEqual(1, command.Turn);
                Assert.AreEqual(1, command.AimValid);
            }
        }
    }
}
