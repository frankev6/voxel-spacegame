using System;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation;
using Game.Simulation.Abilities;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Ai
{
    /// <summary>The nine SPEC-23 AI-006 decision cases, using the real spatial builder and scheduled decision system.</summary>
    public sealed class AiDecisionTests : GameTestBase
    {
        [Test] public void AbilityRules_Fleeing_Boosts()
        {
            using var f = new Fixture();
            var ship = f.Ship(); f.Enemy(new float3(0, 0, 100));
            f.Em.SetComponentData(ship, new ShipStats { MassFraction = .1f });
            var slots = f.Em.AddBuffer<AbilitySlot>(ship);
            slots.Add(new AbilitySlot { AbilityIndex = 1, State = AbilitySlotState.Ready });
            f.Step(10);
            Assert.AreEqual(AiState.Flee, f.Brain(ship).State);
            Assert.AreEqual(1, f.Brain(ship).AbilityEdges);
            Assert.AreEqual(10, f.Brain(ship).AbilityEdgesTick);
            f.Step(11);
            Assert.AreEqual(10, f.Brain(ship).AbilityEdgesTick, "The edge retains its production tick; steering consumes only equal ticks.");
            Assert.AreEqual(1, f.Brain(ship).ThinkCount);
        }

        [Test] public void AbilityRules_RespectsBlocked()
        {
            using var f = new Fixture();
            var ship = f.Ship(); f.Enemy(new float3(0, 0, 100));
            f.Em.SetComponentData(ship, new ShipStats { MassFraction = .1f });
            f.Em.AddBuffer<AbilitySlot>(ship);
            foreach (var state in new[] { AbilitySlotState.Ready, AbilitySlotState.Cooldown })
            {
                var slots = f.Em.GetBuffer<AbilitySlot>(ship); slots.Clear();
                slots.Add(new AbilitySlot { AbilityIndex = 1, State = state, Blocked = (byte)(state == AbilitySlotState.Ready ? 1 : 0) });
                f.Step(10);
                Assert.AreEqual(0, f.Brain(ship).AbilityEdges);
            }
            var buffer = f.Em.GetBuffer<AbilitySlot>(ship); buffer.Clear();
            for (int i = 0; i < 5; i++) buffer.Add(new AbilitySlot { AbilityIndex = (ushort)(i == 4 ? 1 : 2), State = AbilitySlotState.Ready });
            f.Step(20); Assert.AreEqual(0, f.Brain(ship).AbilityEdges, "Only the four command slots can be pressed.");
        }

        [Test] public void ThinkSchedule()
        {
            using var f = new Fixture();
            var active = f.Ship(id: 3); var dormant = f.Ship(id: 3);
            f.Em.AddComponent<Dormant>(dormant);
            var player = f.Ship(); f.Em.AddComponent<PlayerShip>(player);
            var hulk = f.Ship(); f.Em.AddComponent<Hulk>(hulk);
            var spawning = f.Ship(); f.Em.AddComponent<GridSpawning>(spawning);
            int count = 0, slow = 0;
            for (uint tick = 1; tick <= 120; tick++)
            {
                f.Step(tick);
                if ((tick + 3) % 10 == 0) count++;
                if ((tick + 3) % 60 == 0) slow++;
                Assert.AreEqual(count, f.Brain(active).ThinkCount);
                Assert.AreEqual(slow, f.Brain(dormant).ThinkCount);
            }
            Assert.AreEqual(12, count); Assert.AreEqual(2, slow);
            foreach (var excluded in new[] { player, hulk, spawning }) Assert.AreEqual(0, f.Brain(excluded).ThinkCount);
        }

        [Test] public void Target_Scoring()
        {
            using var f = new Fixture();
            var ship = f.Ship();
            var near = f.Enemy(new float3(0, 0, 100));
            var player = f.Enemy(new float3(100, 0, 100)); f.Em.AddComponent<PlayerShip>(player);
            f.Step(10); Assert.AreEqual(player, f.Target(ship).Value, "Player bias outweighs the distance difference.");
            f.Em.RemoveComponent<PlayerShip>(player);
            f.Step(20); Assert.AreEqual(player, f.Target(ship).Value, "Stickiness retains the current target.");
            f.Em.SetComponentData(ship, default(ShipTarget));
            f.Step(30); Assert.AreEqual(near, f.Target(ship).Value);
            Assert.AreEqual(30, f.Target(ship).SinceTick);
            f.Step(40); Assert.AreEqual(30, f.Target(ship).SinceTick);
            var tie = f.Enemy(new float3(0, 0, -100));
            f.Em.SetComponentData(ship, default(ShipTarget));
            f.Step(50); Assert.AreEqual(near.Index < tie.Index ? near : tie, f.Target(ship).Value);
        }

        [Test] public void Target_NeverAlly()
        {
            using var f = new Fixture();
            var ship = f.Ship();
            f.Grid(new float3(5, 0, 0), 0); f.Grid(new float3(10, 0, 0), 2);
            var hulk = f.Enemy(new float3(15, 0, 0)); f.Em.AddComponent<Hulk>(hulk);
            f.Grid(new float3(20, 0, 0), 1, GridKind.Asteroid);
            var target = f.Enemy(new float3(100, 0, 0));
            f.Step(10); Assert.AreEqual(target, f.Target(ship).Value);
            f.Em.AddComponent<Hulk>(target);
            f.Step(20, false); Assert.AreEqual(Entity.Null, f.Target(ship).Value, "Fresh hulk state overrides a previous-tick ship proxy.");
            Assert.AreEqual(0, f.Control(ship).Autofire);
            f.Em.RemoveComponent<Hulk>(target); f.Step(30);
            f.Em.DestroyEntity(target); f.Step(40, false);
            Assert.AreEqual(Entity.Null, f.Target(ship).Value);
        }

        [Test] public void States_Table()
        {
            foreach (var row in new[]
            {
                (AiRole.Combat, 1f, true, AiState.Engage),
                (AiRole.Combat, .1f, true, AiState.Flee),
                (AiRole.Combat, .1f, false, AiState.Patrol),
                (AiRole.Civilian, 1f, true, AiState.Flee),
                (AiRole.Turret, .1f, true, AiState.Hold),
            })
            {
                using var f = new Fixture(row.Item1);
                var ship = f.Ship(); f.Em.SetComponentData(ship, new ShipStats { MassFraction = row.Item2 });
                if (row.Item3) f.Enemy(new float3(0, 0, 100));
                f.Step(10); Assert.AreEqual(row.Item4, f.Brain(ship).State);
                if (row.Item1 == AiRole.Civilian) Assert.AreEqual(Entity.Null, f.Target(ship).Value);
                if (row.Item1 == AiRole.Turret) { Assert.AreEqual(0, f.Brain(ship).GoalSpeedFraction); Assert.AreEqual(float3.zero, f.Brain(ship).GoalPosition); }
            }
            using var unarmed = new Fixture(); var civilian = unarmed.Ship();
            unarmed.Em.SetComponentData(civilian, default(WeaponStats)); unarmed.Enemy(new float3(0, 0, 100));
            unarmed.Step(10); Assert.AreEqual(AiState.Flee, unarmed.Brain(civilian).State);
            using var idle = new Fixture(patrol: 0); var stationary = idle.Ship(); idle.Step(10);
            Assert.AreEqual(AiState.Idle, idle.Brain(stationary).State);
        }

        [Test] public void Leash_Return()
        {
            using var f = new Fixture(); var ship = f.Ship();
            f.Move(ship, new float3(1600, 0, 0)); f.Enemy(new float3(1600, 0, 100));
            f.Step(10); Assert.AreEqual(AiState.Return, f.Brain(ship).State);
            Assert.AreEqual(Entity.Null, f.Target(ship).Value); Assert.AreEqual(float3.zero, f.Brain(ship).GoalPosition);
            f.Move(ship, new float3(500, 0, 0)); f.Step(20);
            Assert.AreEqual(AiState.Return, f.Brain(ship).State, "Return persists inside the leash until arrival.");
            f.Move(ship, new float3(50, 0, 0)); f.Step(30);
            Assert.AreEqual(AiState.Patrol, f.Brain(ship).State);
        }

        [Test] public void FriendlyFire_Blocks()
        {
            using var f = new Fixture(); var ship = f.Ship();
            f.Enemy(new float3(0, 0, 100)); var ally = f.Grid(new float3(0, 0, 50), 0);
            f.Step(10); Assert.AreEqual(1, f.Brain(ship).FireBlocked); Assert.AreEqual(0, f.Control(ship).Autofire);
            f.Move(ally, new float3(20, 0, 50)); f.Step(20);
            Assert.AreEqual(0, f.Brain(ship).FireBlocked); Assert.AreEqual(1, f.Control(ship).Autofire);
            f.Move(ally, new float3(0, 0, 150)); f.Step(30);
            Assert.AreEqual(0, f.Brain(ship).FireBlocked, "An ally beyond the target does not block.");
        }

        [Test] public void Avoidance_ShiftsGoal()
        {
            using var f = new Fixture(); var ship = f.Ship();
            f.Move(ship, new float3(1100, 0, 0));
            f.Step(10); var original = f.Brain(ship).GoalPosition;
            f.Grid(new float3(550, 0, 10), 2);
            f.Step(20); var shifted = f.Brain(ship).GoalPosition;
            Assert.AreEqual(original.x, shifted.x); Assert.Less(shifted.z, original.z);
            Assert.AreEqual(0, shifted.y);
        }

        sealed class Fixture : IDisposable
        {
            readonly World world = new World("AiDecisionTests");
            readonly SystemHandle builder, decisions;
            readonly BlobAssetReference<AiProfilesBlob> profiles;
            readonly FactionRelations factions;
            readonly Entity clock;
            public EntityManager Em => world.EntityManager;
            public Fixture(AiRole role = AiRole.Combat, float patrol = 600)
            {
                Em.CreateEntity(typeof(GameplayActive)); clock = Em.CreateSingleton(new SimClock());
                factions = FactionRelations.CreateDefault(Allocator.Persistent); Em.CreateSingleton(factions);
                using var b = new BlobBuilder(Allocator.Temp);
                ref var root = ref b.ConstructRoot<AiProfilesBlob>();
                var rows = b.Allocate(ref root.Profiles, 1);
                rows[0] = new AiProfile { Role = role, ThinkIntervalTicks = 10, SensorRange = 1000,
                    PreferredRangeFraction = .7f, MinEngageRange = 80, Throttle = .85f, FleeHullFraction = .25f,
                    LeashRadius = 1000, PatrolRadius = patrol, PlayerBias = .3f, Stickiness = .25f,
                    OrbitStepRad = .35f, OrbitFlipThinks = 12, AvoidFriendlyFire = 1 };
                var rules = b.Allocate(ref rows[0].AbilityRules, 1);
                rules[0] = new AiAbilityRuleDef { AbilityIndex = 1, Trigger = AiAbilityTrigger.Fleeing };
                b.Allocate(ref root.Formations, 0);
                profiles = b.CreateBlobAssetReference<AiProfilesBlob>(Allocator.Persistent);
                Em.CreateSingleton(new AiConfig { Profiles = profiles, HashCellSize = 128, ArrivalRadius = 40,
                    AvoidanceMargin = 25, DormantThinkMultiplier = 6 });
                builder = world.GetOrCreateSystem<SpatialHashBuildSystem>();
                decisions = world.GetOrCreateSystem<AiDecisionSystem>();
            }
            public Entity Grid(float3 position, byte faction, GridKind kind = GridKind.Ship)
            {
                var e = Em.CreateEntity(typeof(VoxelGrid), typeof(LocalTransform), typeof(Allegiance));
                Em.SetComponentData(e, new VoxelGrid { Kind = kind, CellSize = 1, CellMin = new int3(-1), CellMax = int3.zero });
                Move(e, position); Em.SetComponentData(e, new Allegiance { Faction = faction }); return e;
            }
            public Entity Enemy(float3 position) => Grid(position, 1);
            public Entity Ship(ulong id = 10)
            {
                var e = Grid(float3.zero, 0);
                Em.AddComponentData(e, new AiBrain { Initialized = 1, OrbitSign = 1 });
                Em.AddComponentData(e, new PersistentId(id));
                Em.AddComponentData(e, default(ShipTarget)); Em.AddComponentData(e, default(WeaponControl));
                Em.AddComponentData(e, new ShipStats { MassFraction = 1 });
                Em.AddComponentData(e, new WeaponStats { MaxWeaponRange = 500 }); return e;
            }
            public void Move(Entity e, float3 position) => Em.SetComponentData(e, LocalTransform.FromPosition(position));
            public AiBrain Brain(Entity e) => Em.GetComponentData<AiBrain>(e);
            public ShipTarget Target(Entity e) => Em.GetComponentData<ShipTarget>(e);
            public WeaponControl Control(Entity e) => Em.GetComponentData<WeaponControl>(e);
            public void Step(uint tick, bool build = true)
            {
                Em.SetComponentData(clock, new SimClock { Tick = tick });
                if (build) builder.Update(world.Unmanaged);
                decisions.Update(world.Unmanaged); Em.CompleteAllTrackedJobs();
                using var query = Em.CreateEntityQuery(ComponentType.ReadOnly<SpatialHash>());
                query.GetSingleton<SpatialHash>().CompleteAccess();
            }
            public void Dispose() { Em.CompleteAllTrackedJobs(); world.Dispose(); profiles.Dispose(); factions.Dispose(); }
        }
    }
}
