using Game.Simulation.Commands;
using Game.Tests.Common;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Commands
{
    /// <summary>SPEC-32 TRV-007: real fixed-step command writes, independent lock masks and no action replay.</summary>
    public sealed class PlayerControlLockTests : GameTestBase
    {
        EcsTestWorld world;
        Entity input, ship, lockEntity;
        EntityManager EM => world.EntityManager;
        const ShipCommandEdges AllEdges = (ShipCommandEdges)511;

        [SetUp] public void SetUp()
        {
            world = CreateWorld(typeof(PlayerCommandApplySystem));
            world.AddGameplayActive();
            input = world.AddSingleton(default(PlayerCommand));
            ship = EM.CreateEntity(typeof(PlayerShip), typeof(ShipCommand));
            using var edges = EM.CreateEntityQuery(typeof(PlayerEdgeConsumed));
            lockEntity = edges.GetSingletonEntity();
            Assert.IsTrue(EM.HasComponent<PlayerControlLock>(lockEntity), "The lock belongs on the edge-consumer singleton entity.");
            using var locks = EM.CreateEntityQuery(typeof(PlayerControlLock));
            Assert.AreEqual(1, locks.CalculateEntityCount());
            Assert.AreEqual(PlayerControlLockFlags.None, EM.GetComponentData<PlayerControlLock>(lockEntity).Flags);
        }

        void Lock(PlayerControlLockFlags flags) => EM.SetComponentData(lockEntity, new PlayerControlLock { Flags = flags });
        void Set(PlayerCommand command) => EM.SetComponentData(input, command);
        ShipCommand Tick() { world.StepTicks(1); return EM.GetComponentData<ShipCommand>(ship); }
        static PlayerEdgeCounters Counters(byte value, byte cruise = 0) => new PlayerEdgeCounters
        {
            Interact = value, Ability1 = value, Ability2 = value, Ability3 = value, Ability4 = value,
            TargetNext = value, TargetUnderCursor = value, TargetClear = value, AutofireToggle = value, Cruise = cruise
        };
        static PlayerCommand Sample(byte edges = 1) => new PlayerCommand
        {
            Thrust = new float2(-.5f, -.75f), Turn = .625f, // strafe + reverse are meaningful while unlocked.
            AimWorld = new float3(12, 0, -34), AimValid = 1,
            Flags = ShipCommandFlags.Boost, FireGroups = 0x15u, Edges = Counters(edges), FrameIndex = 91
        };

        [Test] public void Movement_BrakesOnly()
        {
            var sample = Sample(); Set(sample);
            var ai = EM.CreateEntity(typeof(ShipCommand));
            var aiCommand = new ShipCommand { Thrust = new float2(.25f, 1), Turn = -.25f, FireGroups = 9 };
            EM.SetComponentData(ai, aiCommand);
            Lock(PlayerControlLockFlags.Movement);
            var command = Tick();
            Assert.AreEqual(float2.zero, command.Thrust);
            Assert.AreEqual(0, command.Turn);
            Assert.AreEqual(ShipCommandFlags.Brake, command.Flags);
            Assert.AreEqual(sample.AimWorld, command.AimWorld);
            Assert.AreEqual(sample.AimValid, command.AimValid);
            Assert.AreEqual(sample.FireGroups, command.FireGroups, "Movement does not lock actions.");
            Assert.AreEqual(AllEdges, command.Edges);
            Assert.AreEqual(aiCommand, EM.GetComponentData<ShipCommand>(ai), "The player lock cannot write AI commands.");
            InputUnchanged(sample, EM.GetComponentData<PlayerCommand>(input));
            Lock(PlayerControlLockFlags.None);
            command = Tick();
            Assert.AreEqual(sample.Thrust, command.Thrust);
            Assert.AreEqual(sample.Turn, command.Turn);
            Assert.AreEqual(sample.Flags, command.Flags);
            Assert.AreEqual(sample.FireGroups, command.FireGroups);
        }

        [Test] public void Actions_DropsEdges()
        {
            var sample = Sample(3); Set(sample);
            Lock(PlayerControlLockFlags.Actions);
            var command = Tick();
            Assert.AreEqual(0u, command.FireGroups);
            Assert.AreEqual(ShipCommandEdges.None, command.Edges);
            Assert.AreEqual(sample.Thrust, command.Thrust);
            Assert.AreEqual(sample.Turn, command.Turn);
            Assert.AreEqual(sample.Flags, command.Flags, "Actions does not impose braking.");
            Assert.AreEqual(sample.AimWorld, command.AimWorld);
            Assert.AreEqual(sample.AimValid, command.AimValid);
            Assert.AreEqual(sample.Edges, world.GetSingleton<PlayerEdgeConsumed>().Value);
            InputUnchanged(sample, EM.GetComponentData<PlayerCommand>(input));
        }

        [Test] public void NoReplayAfterUnlock()
        {
            // Three presses cross byte wrap between locked ticks: 254 -> 255 -> 0 -> 1.
            EM.SetComponentData(lockEntity, new PlayerEdgeConsumed { Value = Counters(254, 23) });
            var sample = Sample(); sample.Edges.Cruise = 37; Set(sample);
            Lock(PlayerControlLockFlags.Movement | PlayerControlLockFlags.Actions);
            var command = Tick();
            Assert.AreEqual(float2.zero, command.Thrust);
            Assert.AreEqual(0, command.Turn);
            Assert.AreEqual(ShipCommandFlags.Brake, command.Flags);
            Assert.AreEqual(ShipCommandEdges.None, command.Edges);
            Assert.AreEqual(0u, command.FireGroups);
            Assert.AreEqual(Counters(1, 23), world.GetSingleton<PlayerEdgeConsumed>().Value, "Discard every ship press; preserve the travel-owned Cruise counter.");
            Lock(PlayerControlLockFlags.None);
            for (int tick = 0; tick < 3; tick++)
            {
                command = Tick();
                Assert.AreEqual(ShipCommandEdges.None, command.Edges, "A locked press must never replay after unlock.");
                Assert.AreEqual(sample.FireGroups, command.FireGroups, "Held fire resumes through the normal unlocked path.");
            }
            sample.Edges = Counters(2, 37); Set(sample);
            Assert.AreEqual(AllEdges, Tick().Edges, "New unlocked presses still fire normally.");
            Assert.AreEqual(ShipCommandEdges.None, Tick().Edges);
            Assert.AreEqual(23, world.GetSingleton<PlayerEdgeConsumed>().Value.Cruise);
        }

        [Test] public void None_Unchanged()
        {
            foreach (var sample in new[]
            {
                Sample(2),
                new PlayerCommand { Thrust = new float2(-0f, -1), Turn = -1, AimWorld = new float3(-1, 2, 3),
                    AimValid = 1, Flags = ShipCommandFlags.Brake | ShipCommandFlags.Boost, FireGroups = uint.MaxValue, Edges = Counters(1) },
                new PlayerCommand { Thrust = new float2(float.NaN, -3), Turn = float.PositiveInfinity,
                    AimWorld = new float3(2, float.NaN, 3), AimValid = 1, FireGroups = 17, Edges = Counters(1) }
            })
            {
                Set(sample);
                EM.RemoveComponent<PlayerControlLock>(lockEntity);
                EM.SetComponentData(lockEntity, default(PlayerEdgeConsumed));
                var unlocked = Tick();
                var consumed = world.GetSingleton<PlayerEdgeConsumed>();
                EM.AddComponentData(lockEntity, new PlayerControlLock { Flags = PlayerControlLockFlags.None });
                EM.SetComponentData(lockEntity, default(PlayerEdgeConsumed));
                var none = Tick();
                BitwiseEqual(unlocked, none);
                Assert.AreEqual(consumed, world.GetSingleton<PlayerEdgeConsumed>(), "None must preserve ordinary one-counter-step consumption.");
                InputUnchanged(sample, EM.GetComponentData<PlayerCommand>(input));
            }
        }

        static unsafe void BitwiseEqual(ShipCommand expected, ShipCommand actual)
            => Assert.AreEqual(0, UnsafeUtility.MemCmp(&expected, &actual, UnsafeUtility.SizeOf<ShipCommand>()), "None changed the command bytes.");

        static void InputUnchanged(PlayerCommand expected, PlayerCommand actual)
        {
            // Compare floating-point bits so unchanged NaNs/signed zero do not fail value equality.
            Assert.AreEqual(math.asuint(expected.Thrust), math.asuint(actual.Thrust));
            Assert.AreEqual(math.asuint(expected.Turn), math.asuint(actual.Turn));
            Assert.AreEqual(math.asuint(expected.AimWorld), math.asuint(actual.AimWorld));
            Assert.AreEqual(expected.AimValid, actual.AimValid);
            Assert.AreEqual(expected.Flags, actual.Flags);
            Assert.AreEqual(expected.FireGroups, actual.FireGroups);
            Assert.AreEqual(expected.Edges, actual.Edges);
            Assert.AreEqual(expected.FrameIndex, actual.FrameIndex);
        }
    }
}
