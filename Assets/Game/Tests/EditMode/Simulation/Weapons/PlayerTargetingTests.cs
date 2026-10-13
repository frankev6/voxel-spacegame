using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Tests.EditMode.Simulation.Weapons
{
    /// <summary>The six WPN-005 cases, through the real input bridge, targeting group and guarded spatial builder.</summary>
    public sealed class PlayerTargetingTests : GameTestBase
    {
        bool priorSafety;
        [SetUp] public void SetUp() { priorSafety = BurstCompiler.Options.EnableBurstSafetyChecks; BurstCompiler.Options.EnableBurstSafetyChecks = true; }
        [TearDown] public void TearDown() => BurstCompiler.Options.EnableBurstSafetyChecks = priorSafety;

        [Test] public void Next_CyclesByDistance()
        {
            using var f = new Fixture();
            var far = f.Grid(new float3(100, 0, 0));
            var near = f.Grid(new float3(20, 0, 0));
            var tiedFirst = f.Grid(new float3(40.004f, 0, 0));
            var tiedSecond = f.Grid(new float3(40.001f, 0, 0));
            f.Build();
            foreach (var expected in new[] { near, tiedFirst, tiedSecond, far, near })
            {
                f.Tick(ShipCommandEdges.TargetNext);
                Assert.AreEqual(expected, f.Target.Value);
                Assert.AreEqual(f.TickCount, f.Target.SinceTick);
                uint selectedAt = f.Target.SinceTick;
                f.Tick();
                Assert.AreEqual(expected, f.Target.Value); Assert.AreEqual(selectedAt, f.Target.SinceTick, "A counter press is consumed once.");
            }
            // Selection remains complete when the hash output holds only the lowest 127 proxy indices.
            for (int i = 0; i < 140; i++) f.Grid(new float3(200 + i, 0, 0));
            var overflowNearest = f.Grid(new float3(1, 0, 0));
            f.Target = default; f.Build(); f.Tick(ShipCommandEdges.TargetNext);
            Assert.AreEqual(overflowNearest, f.Target.Value);
            // Half-centimetre ties use round-to-nearest-even before the entity-index tie break.
            using var rounded = new Fixture();
            var evenTieFirst = rounded.Grid(new float3(.125f, 0, 0));
            var actuallyNearer = rounded.Grid(new float3(.124f, 0, 0));
            rounded.Build(); rounded.Tick(ShipCommandEdges.TargetNext);
            Assert.AreEqual(evenTieFirst, rounded.Target.Value);
            rounded.Tick(ShipCommandEdges.TargetNext); Assert.AreEqual(actuallyNearer, rounded.Target.Value);
        }

        [Test] public void Next_IgnoresNonHostile()
        {
            using var f = new Fixture();
            f.Grid(new float3(1, 0, 0), Allegiance.Player);
            f.Grid(new float3(2, 0, 0), new Allegiance { Faction = 0, Team = 9 });
            f.Grid(new float3(3, 0, 0), new Allegiance { Faction = 2 });
            f.Grid(new float3(4, 0, 0), Allegiance.None);
            f.Grid(new float3(5, 0, 0), kind: GridKind.Asteroid);
            var becameHulk = f.Grid(new float3(6, 0, 0));
            var hulk = f.Grid(new float3(7, 0, 0)); f.Em.AddComponent<Hulk>(hulk);
            f.Body(new float3(8, 0, 0), 3);
            f.Grid(new float3(1001, 0, 0), radius: 100); // sphere overlaps the query, centre exceeds MaxTargetRange
            var station = f.Grid(new float3(100, 0, 0), kind: GridKind.Station);
            f.Build();
            f.Em.AddComponent<Hulk>(becameHulk); // the previous-tick proxy still says Ship
            f.Tick(ShipCommandEdges.TargetNext); Assert.AreEqual(station, f.Target.Value);
            uint since = f.Target.SinceTick;
            f.Tick(ShipCommandEdges.TargetNext); Assert.AreEqual(station, f.Target.Value); Assert.AreEqual(since, f.Target.SinceTick);
            f.Em.DestroyEntity(station);
            f.Tick(ShipCommandEdges.TargetNext); Assert.AreEqual(Entity.Null, f.Target.Value, "No eligible hostile clears the target.");
        }

        [Test] public void UnderCursor_Nearest()
        {
            using var f = new Fixture();
            float3 point = new float3(500, 20, 100);
            f.Em.SetComponentData(f.Player, LocalTransform.FromPosition(point));
            f.Grid(point + new float3(8, 0, 0), radius: 1);
            var broadAlly = f.Grid(point + new float3(20, 0, 0), Allegiance.Player, radius: 16);
            f.Grid(point + new float3(14, 0, 0), new Allegiance { Faction = 2 }, radius: 10);
            f.Grid(point + new float3(5, 0, 0), Allegiance.None, GridKind.Asteroid, 1);
            f.Body(point + new float3(1, 0, 0), 2);
            f.Build(); f.Tick(ShipCommandEdges.TargetUnderCursor, point);
            Assert.AreEqual(broadAlly, f.Target.Value, "Compare sphere-surface distance; equal scores use Entity.Index, and exclude self/bodies.");
            var hulk = f.Grid(point + new float3(7, 0, 0), radius: 4); f.Em.AddComponent<Hulk>(hulk);
            f.Build(); f.Tick(ShipCommandEdges.TargetUnderCursor, point); Assert.AreEqual(hulk, f.Target.Value, "Manual selection permits hulks.");
            f.Tick(); Assert.AreEqual(hulk, f.Target.Value, "An existing manual hulk target remains valid.");
            var insideFirst = f.Grid(point, Allegiance.None, GridKind.Asteroid, 2);
            f.Grid(point + new float3(1, 0, 0), radius: 5);
            f.Build(); f.Tick(ShipCommandEdges.TargetUnderCursor, point);
            Assert.AreEqual(insideFirst, f.Target.Value, "Containing spheres have distance zero, tied by Entity.Index.");
            uint since = f.Target.SinceTick;
            f.Tick(ShipCommandEdges.TargetUnderCursor, new float3(-10000), 0);
            Assert.AreEqual(insideFirst, f.Target.Value); Assert.AreEqual(since, f.Target.SinceTick, "Invalid aim preserves the selection.");
            f.Tick(ShipCommandEdges.TargetUnderCursor, new float3(-10000)); Assert.AreEqual(Entity.Null, f.Target.Value);

            using var dense = new Fixture();
            for (int i = 0; i < 140; i++) dense.Grid(new float3(25, 0, 0), radius: .5f);
            var lateNearest = dense.Grid(new float3(2, 0, 0));
            dense.Build(); dense.Tick(ShipCommandEdges.TargetUnderCursor, float3.zero, complete: false);
            dense.Build(); // the next writer must wait for the still-scheduled targeting reader through GuardedHandles
            Assert.AreEqual(lateNearest, dense.Target.Value, "Cursor overflow must preserve the true nearest, not only the first 127.");
        }

        [Test] public void Clear()
        {
            using var f = new Fixture();
            var near = f.Grid(new float3(20, 0, 0));
            var cursor = f.Grid(new float3(200, 0, 0), Allegiance.Player);
            f.Build(); f.Target = new ShipTarget { Value = near, SinceTick = 0 };
            f.Control = new WeaponControl { Autofire = 1, AimMode = WeaponAimMode.TargetLead, Reserved = 9 };
            f.Tick(ShipCommandEdges.TargetClear);
            Assert.AreEqual(Entity.Null, f.Target.Value); Assert.AreEqual(f.TickCount, f.Target.SinceTick);
            Assert.AreEqual(1, f.Control.Autofire); Assert.AreEqual(WeaponAimMode.TargetLead, f.Control.AimMode); Assert.AreEqual(9, f.Control.Reserved);
            uint since = f.Target.SinceTick;
            f.Tick(ShipCommandEdges.TargetClear); Assert.AreEqual(since, f.Target.SinceTick, "Clearing an already-empty target is not a change.");
            f.Tick(ShipCommandEdges.TargetClear | ShipCommandEdges.TargetNext); Assert.AreEqual(near, f.Target.Value);
            f.Tick(ShipCommandEdges.TargetClear | ShipCommandEdges.TargetNext | ShipCommandEdges.TargetUnderCursor, new float3(200, 0, 0));
            Assert.AreEqual(cursor, f.Target.Value, "Edges execute in the spec's clear/next/cursor order.");
            var before = f.Target; f.Em.AddComponent<Hulk>(f.Player);
            f.Tick(ShipCommandEdges.TargetClear); Assert.AreEqual(before, f.Target, "Player hulks are excluded from command application and targeting.");
            f.Em.RemoveComponent<Hulk>(f.Player); f.Tick(); Assert.AreEqual(before, f.Target, "The ignored press is not replayed after revival.");
            var order = f.CommandOrder();
            Assert.Less(order.IndexOf(typeof(PlayerCommandApplySystem)), order.IndexOf(typeof(PlayerTargetingSystem)));
            Assert.Less(order.IndexOf(typeof(PlayerTargetingSystem)), order.IndexOf(typeof(AiAnchorShiftSystem)));
        }

        [Test] public void Autofire_Toggles()
        {
            using var f = new Fixture();
            var npc = f.Grid(new float3(30, 0, 0));
            var npcCommand = new ShipCommand { Edges = ShipCommandEdges.AutofireToggle, Thrust = new float2(.2f, .3f) };
            var npcControl = new WeaponControl { Autofire = 1, AimMode = WeaponAimMode.TargetLead, Reserved = 21 };
            f.Em.AddComponentData(npc, npcCommand); f.Em.AddComponentData(npc, npcControl); f.Em.AddComponentData(npc, default(ShipTarget));
            f.Control = new WeaponControl { Reserved = 17 };
            f.Build(); f.Tick(ShipCommandEdges.AutofireToggle);
            Assert.AreEqual(1, f.Control.Autofire); Assert.AreEqual(WeaponAimMode.TargetLead, f.Control.AimMode);
            Assert.AreEqual(Entity.Null, f.Target.Value, "Autofire can remain armed without a target.");
            f.Tick(); Assert.AreEqual(1, f.Control.Autofire, "The same input counter is not toggled twice.");
            f.Tick(ShipCommandEdges.AutofireToggle); Assert.AreEqual(0, f.Control.Autofire); Assert.AreEqual(WeaponAimMode.AimPoint, f.Control.AimMode);
            f.Tick(ShipCommandEdges.AutofireToggle); Assert.AreEqual(1, f.Control.Autofire); Assert.AreEqual(17, f.Control.Reserved);
            Assert.AreEqual(npcCommand, f.Em.GetComponentData<ShipCommand>(npc));
            Assert.AreEqual(npcControl, f.Em.GetComponentData<WeaponControl>(npc), "AI commands/control have separate writers.");
        }

        [Test] public void DeadTarget_Cleared()
        {
            using var f = new Fixture();
            var dead = f.Grid(new float3(10, 0, 0));
            var live = f.Grid(new float3(20, 0, 0));
            f.Build(); f.Target = new ShipTarget { Value = dead, SinceTick = 0 };
            f.Em.DestroyEntity(dead);
            var replacement = f.Em.CreateEntity(); Assert.AreNotEqual(dead, replacement);
            f.Tick(); Assert.AreEqual(Entity.Null, f.Target.Value); Assert.AreEqual(f.TickCount, f.Target.SinceTick);
            uint since = f.Target.SinceTick;
            f.Tick(); Assert.AreEqual(since, f.Target.SinceTick);
            f.Tick(ShipCommandEdges.TargetNext); Assert.AreEqual(live, f.Target.Value, "The stale proxy's entity version cannot select a replacement.");
            f.Em.DestroyEntity(live);
            f.Tick(ShipCommandEdges.TargetUnderCursor, new float3(20, 0, 0)); Assert.AreEqual(Entity.Null, f.Target.Value);
        }

        sealed class Fixture : IDisposable
        {
            readonly World world = new World("Player targeting");
            readonly SystemHandle builder;
            readonly ShipCommandGroup commands;
            readonly Entity input, clock;
            FactionRelations factions;
            PlayerEdgeCounters counters;
            public EntityManager Em => world.EntityManager;
            public Entity Player { get; }
            public uint TickCount { get; private set; }
            public ShipTarget Target { get => Em.GetComponentData<ShipTarget>(Player); set => Em.SetComponentData(Player, value); }
            public WeaponControl Control { get => Em.GetComponentData<WeaponControl>(Player); set => Em.SetComponentData(Player, value); }

            public Fixture()
            {
                try
                {
                    Em.CreateEntity(typeof(GameplayActive)); clock = Em.CreateSingleton(default(SimClock));
                    input = Em.CreateSingleton(default(PlayerCommand));
                    Em.CreateSingleton(new WeaponTuning { MaxTargetRange = 1000, UnderCursorRadius = 30, MaxCandidates = 4 });
                    factions = FactionRelations.CreateDefault(Allocator.Persistent); Em.CreateSingleton(factions);
                    Em.AddBuffer<RootComponentRegistration>(Em.CreateEntity()); Em.CreateSingleton(default(OriginShiftRecord));
                    builder = world.CreateSystem<SpatialHashBuildSystem>();
                    commands = world.GetOrCreateSystemManaged<ShipCommandGroup>();
                    // Add in reverse order: SortSystems must use the bucket and the newly implemented targeting→anchor edge.
                    commands.AddSystemToUpdateList(world.CreateSystem<AiAnchorShiftSystem>());
                    commands.AddSystemToUpdateList(world.CreateSystem<PlayerTargetingSystem>());
                    commands.AddSystemToUpdateList(world.CreateSystem<PlayerCommandApplySystem>());
                    commands.SortSystems();
                    Player = Grid(float3.zero, Allegiance.Player);
                    Em.AddComponent<PlayerShip>(Player); Em.AddComponent<ShipCommand>(Player);
                    Em.AddComponent<ShipTarget>(Player); Em.AddComponent<WeaponControl>(Player);
                }
                catch { world.Dispose(); factions.Dispose(); throw; }
            }

            public Entity Grid(float3 center, Allegiance? allegiance = null, GridKind kind = GridKind.Ship, float radius = 1)
            {
                Entity entity = Em.CreateEntity(typeof(VoxelGrid), typeof(LocalTransform), typeof(Allegiance), typeof(ShieldState));
                Em.SetComponentData(entity, new VoxelGrid { CellMin = new int3(-1), CellMax = int3.zero, CellSize = .1f, Kind = kind });
                Em.SetComponentData(entity, LocalTransform.FromPosition(center));
                Em.SetComponentData(entity, allegiance ?? new Allegiance { Faction = 1 });
                // Centred grid bounds make Center equal the pose; an analytic shield gives the exact requested proxy radius.
                Em.SetComponentData(entity, new ShieldState { MaxHp = 1, Active = 1, BoundingRadius = radius });
                return entity;
            }
            public Entity Body(float3 center, float radius)
            {
                Entity entity = Em.CreateEntity(typeof(SpatialBody), typeof(LocalTransform));
                Em.SetComponentData(entity, new SpatialBody { Radius = radius }); Em.SetComponentData(entity, LocalTransform.FromPosition(center));
                return entity;
            }
            public void Build()
            {
                builder.Update(world.Unmanaged);
                using var query = Em.CreateEntityQuery(ComponentType.ReadOnly<SpatialHash>());
                query.GetSingleton<SpatialHash>().CompleteAccess(); Em.CompleteAllTrackedJobs();
            }
            public void Tick(ShipCommandEdges edges = ShipCommandEdges.None, float3 aim = default, byte aimValid = 1, bool complete = true)
            {
                if ((edges & ShipCommandEdges.TargetNext) != 0) counters.TargetNext = unchecked((byte)(counters.TargetNext + 1));
                if ((edges & ShipCommandEdges.TargetUnderCursor) != 0) counters.TargetUnderCursor = unchecked((byte)(counters.TargetUnderCursor + 1));
                if ((edges & ShipCommandEdges.TargetClear) != 0) counters.TargetClear = unchecked((byte)(counters.TargetClear + 1));
                if ((edges & ShipCommandEdges.AutofireToggle) != 0) counters.AutofireToggle = unchecked((byte)(counters.AutofireToggle + 1));
                Em.SetComponentData(input, new PlayerCommand { Edges = counters, AimWorld = aim, AimValid = aimValid });
                Em.SetComponentData(clock, new SimClock { Tick = ++TickCount });
                world.SetTime(new TimeData(TickCount / 60d, 1f / 60f)); commands.Update();
                if (complete) Em.CompleteAllTrackedJobs();
            }
            public List<Type> CommandOrder()
            {
                var result = new List<Type>();
                using var handles = commands.GetAllSystems(Allocator.Temp);
                foreach (var handle in handles) result.Add(TypeManager.GetSystemType(world.Unmanaged.GetSystemTypeIndex(handle)));
                return result;
            }
            public void Dispose() { Em.CompleteAllTrackedJobs(); world.Dispose(); factions.Dispose(); }
        }
    }
}
