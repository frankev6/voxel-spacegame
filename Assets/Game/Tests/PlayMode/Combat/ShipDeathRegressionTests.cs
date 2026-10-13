using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Simulation;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using FlightFixture = Game.Tests.PlayMode.Simulation.Flight.FlightScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Combat
{
    internal struct RegressionKillScript : IComponentData { public uint Tick; }
    internal struct RegressionKillTarget : IBufferElementData { public Entity Root; }

    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(ImpactEventSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    internal partial struct RegressionCoreKillSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        { state.RequireForUpdate<RegressionKillScript>(); state.RequireForUpdate<RegressionKillTarget>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<SimClock>(); }
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.GetSingleton<SimClock>().Tick != SystemAPI.GetSingleton<RegressionKillScript>().Tick) return;
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            var writer = streams.Damage.BeginWrite(ref state, ProducerKind.Test, 1);
            state.Dependency = new KillJob { Targets = SystemAPI.GetSingletonBuffer<RegressionKillTarget>(true).AsNativeArray(), Writer = writer }.Schedule(state.Dependency);
            streams.Damage.EndWrite(state.Dependency);
        }
        [BurstCompile] struct KillJob : IJob
        {
            [ReadOnly] public NativeArray<RegressionKillTarget> Targets;
            public NativeStream.Writer Writer;
            public void Execute()
            {
                Writer.BeginForEachIndex(0);
                for (int i = 0; i < Targets.Length; i++)
                    Writer.Write(new DamageEvent { Grid = Targets[i].Root, Source = Targets[(i + 1) % Targets.Length].Root,
                        LocalPoint = new float3(1.25f,.75f,1.25f), LocalDir = new float3(0,1,0), Amount = 100000,
                        Type = DamageType.Kinetic, Producer = ProducerKind.Test, Sequence = (uint)i, Flags = DamageEventFlags.NoImpulse });
                Writer.EndForEachIndex();
            }
        }
    }

    /// <summary>Test-only readback/drain after all production jobs, shared with the ten-minute soak.</summary>
    internal static unsafe class DamageScenarioProbe
    {
        public static DamageTickStats Observe(EcsTestWorld world, out int liveChunks)
        {
            var em = world.EntityManager; var streams = world.GetSingleton<DamageStreams>(); streams.Data->AllJobs(default).Complete();
            var stats = streams.Data->Current; liveChunks = 0;
            using (var query = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<ChunkBody>() }, Options = EntityQueryOptions.IncludeDisabledEntities }))
            using (var chunks = query.ToComponentDataArray<ChunkBody>(Allocator.Temp))
            {
                foreach (var chunk in chunks) if (chunk.Live != 0) liveChunks++;
                Assert.LessOrEqual(chunks.Length, 64, "Pool entities, including disabled slots, must stay within the configured cap.");
            }
            Assert.LessOrEqual(liveChunks, 64, "Observed after every fixed tick, after GameplayEnd ECB playback.");
            var fx = world.GetSingleton<FxEventQueue>(); fx.Access.CompleteAccess(); fx.Queue.Clear();
            var sfx = world.GetSingleton<SfxEventQueue>(); sfx.Access.CompleteAccess(); sfx.Queue.Clear();
            return stats;
        }
        public static Entity Request(EntityManager em, Entity root, GridDissolveMode mode)
        {
            var entity = em.CreateEntity(typeof(GridDissolveRequest)); em.SetComponentData(entity, new GridDissolveRequest { Grid = root, Mode = mode }); return entity;
        }
        public static void WarmUp()
        {
            using var f = new DamageDeterminismTests.Fixture(true); f.Begin();
            for (int i = 0; i < 360; i++) { f.Flight.Tick(); Observe(f.Flight.Test, out _); }
        }
    }

    /// <summary>Isolates the global native leak probe from unrelated player-loop Worlds; restores every flag even on failure.</summary>
    internal sealed class DamageLeakScope : IDisposable
    {
        readonly List<(ComponentSystemGroup Group, bool Enabled)> groups = new List<(ComponentSystemGroup, bool)>();
        readonly NativeLeakDetectionMode leakMode = NativeLeakDetection.Mode;
        readonly bool ignoreLogs = LogAssert.ignoreFailingMessages;
        public DamageLeakScope()
        {
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace; LogAssert.ignoreFailingMessages = false;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            Pause(world.GetExistingSystemManaged<InitializationSystemGroup>());
            Pause(world.GetExistingSystemManaged<SimulationSystemGroup>());
            Pause(world.GetExistingSystemManaged<PresentationSystemGroup>());
        }
        void Pause(ComponentSystemGroup group)
        { if (group != null) { groups.Add((group, group.Enabled)); group.Enabled = false; } }
        public void Dispose()
        {
            NativeLeakDetection.Mode = leakMode; LogAssert.ignoreFailingMessages = ignoreLogs;
            foreach (var item in groups) if (item.Group.World != null && item.Group.World.IsCreated) item.Group.Enabled = item.Enabled;
        }
    }

    /// <summary>DMG-015 R25/R26: actual ram contacts and real weapon firing overlap ordered split/death/despawn ECB playback.</summary>
    [Category("Scenario")]
    public sealed class ShipDeathRegressionTests
    {
        static FlightFixture Create(bool firing)
        {
            var systems = new List<Type> { typeof(DamageStreamsSystem), typeof(ImpactEventSystem), typeof(DamageResolveSystem),
                typeof(IntegritySystem), typeof(SplitSystem), typeof(ShipDeathSystem), typeof(ChunkColliderCleanupSystem),
                typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem), typeof(ExplosionSystem), typeof(SplitGraceContactSystem) };
            if (firing) systems.AddRange(new[] { typeof(WeaponInitSystem), typeof(WeaponAimSystem), typeof(WeaponFireSystem),
                typeof(PowerRegistrationSystem), typeof(PowerInitSystem), typeof(PowerAllocationSystem), typeof(ShipStatsAggregateSystem), typeof(RegressionCoreKillSystem) });
            var f = new FlightFixture(systems.ToArray());
            f.Em.CreateSingleton(new PersistentIdAllocator { Next = 1000, End = 100000 });
            f.Em.CreateSingleton(new WeaponTuning { MaxCandidates = 32, ExplosionImpulsePerDamage = 2, FixedConvergenceRad = .1f });
            return f;
        }
        static VoxelStructure RamShip(FlightFixture f)
        {
            var source = new VoxelStructure(.5f); int3 core = new int3(19,0,4);
            for (int x = 0; x < 20; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 10; z++)
                if (!math.all(new int3(x,y,z) == core))
                    Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = f.Hull, Origin = new int3(x,y,z) }, f.Content));
            Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = f.Index("block_core_small"), Origin = core }, f.Content));
            Assert.AreEqual(400, source.SolidCellCount); return source;
        }

        [UnityTest] public IEnumerator R25_MassRamming_NoEcbErrors()
        {
            using var leak = new DamageLeakScope(); DamageScenarioProbe.WarmUp(); LogAssert.NoUnexpectedReceived(); UnsafeUtility.CheckForLeaks();
            yield return null; // Keep the leak probe's diagnostic Log output out of the gameplay log assertion.
            int deaths = 0, rams = 0;
            using (var f = Create(false))
            {
                var roots = new Entity[63]; var bases = new float3[63];
                for (int i = 0; i < roots.Length; i++)
                {
                    int pair = i / 2; bases[i] = new float3((pair % 7) * 28,0,(pair / 7) * 12);
                    if (i == 62) bases[i] = bases[0];
                    // Every requested ship overlaps another. Immunity protects asynchronous setup only.
                    roots[i] = f.SpawnSource(RamShip(f), bases[i] + new float3(i % 2 == 0 ? 0 : 9.6f,0,0));
                    f.Em.AddComponent<DamageImmune>(roots[i]);
                }
                f.Ready(roots);
                for (int i = 0; i < roots.Length; i++)
                {
                    f.Em.RemoveComponent<DamageImmune>(roots[i]); bool right = i % 2 != 0;
                    f.Em.SetComponentData(roots[i], LocalTransform.FromPositionRotation(bases[i] + new float3(right ? 19.6f : 0,0,right ? 4.5f : 0), right ? quaternion.RotateY(math.PI) : quaternion.identity));
                    f.Em.SetComponentData(roots[i], new PhysicsVelocity { Linear = new float3(right ? -50 : 50,0,0) });
                    f.Em.SetComponentData(roots[i], new PersistentId { Value = (ulong)(100 + i) });
                }
                uint start = f.Test.TicksRun;
                for (int i = 0; i < 600; i++) { f.Tick(); rams += DamageScenarioProbe.Observe(f.Test, out _).RamEvents; }
                Assert.AreEqual(600u, f.Test.TicksRun - start);
                foreach (var root in roots)
                    if (f.Em.Exists(root) && f.Em.HasComponent<HulkInfo>(root) && f.Em.GetComponentData<HulkInfo>(root).Cause == HulkCause.CoreLost) deaths++;
                Assert.Greater(rams, 0, "Real Unity Physics contacts must enter ImpactEventSystem; no scripted core kills in R25.");
                Assert.GreaterOrEqual(deaths, 10, "At least ten original ships must become core-loss hulks through ram damage.");
            }
            LogAssert.NoUnexpectedReceived(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "R25 must release all scenario native allocations.");
            TestContext.WriteLine($"DMG-015 R25: 63 × 400-cell ships, 600 ticks, ram events={rams}, original core-loss hulks={deaths}, native leaks=0.");
        }

        [UnityTest] public IEnumerator R26_TwelveShipDeaths_NoEcbErrors()
        {
            using var leak = new DamageLeakScope(); DamageScenarioProbe.WarmUp(); LogAssert.NoUnexpectedReceived(); UnsafeUtility.CheckForLeaks();
            yield return null;
            var report = new ValidationReport("R26 weapon definitions");
            var catalog = ContentCatalog.Build(Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase.Definitions, report);
            Assert.IsFalse(report.HasErrors, report.ToText());
            var weapons = WeaponContent.BuildBlob(catalog); FactionRelations factions = default; uint fired = 0; int shots = 0;
            try
            {
                using var f = Create(true); f.Em.CreateSingleton(new WeaponDefsRef { Value = weapons });
                factions = FactionRelations.CreateDefault(Allocator.Persistent); f.Em.CreateSingleton(factions);
                var roots = new Entity[12];
                for (int i = 0; i < roots.Length; i++) roots[i] = f.SpawnSource(DamageDeterminismTests.Fixture.Build(f), new float3(i * 200,0,0));
                f.Ready(roots);
                for (int i = 0; i < roots.Length; i++)
                {
                    var enemy = new Allegiance { Faction = 1, Team = 1 }; Assert.IsTrue(factions.IsHostile(enemy, Allegiance.Player));
                    if (f.Em.HasComponent<Allegiance>(roots[i])) f.Em.SetComponentData(roots[i], enemy); else f.Em.AddComponentData(roots[i], enemy);
                    f.Em.SetComponentData(roots[i], new ShipCommand { FireGroups = uint.MaxValue, AimValid = 1, AimWorld = new float3(i * 200 + 7.25f,.75f,100) });
                }
                uint start = f.Test.GetSingleton<SimClock>().Tick;
                var script = f.Em.CreateSingleton(new RegressionKillScript { Tick = start + 120 });
                var targets = f.Em.AddBuffer<RegressionKillTarget>(script); foreach (var root in roots) targets.Add(new RegressionKillTarget { Root = root });
                var members = new List<Entity>();
                for (int tick = 0; tick < 600; tick++)
                {
                    f.Tick(); DamageScenarioProbe.Observe(f.Test, out _);
                    var released = f.Test.GetSingleton<WeaponShots>(); released.Access.CompleteAccess(); shots += released.Shots.Length;
                    foreach (var shot in released.Shots) for (int i = 0; i < roots.Length; i++) if (shot.Grid == roots[i]) fired |= 1u << i;
                    if (tick != 120) continue;
                    foreach (var root in roots)
                    {
                        Assert.IsTrue(f.Em.HasComponent<Hulk>(root)); Assert.AreEqual(GridKind.Hulk, f.Em.GetComponentData<VoxelGrid>(root).Kind);
                        Assert.AreEqual(HulkCause.CoreLost, f.Em.GetComponentData<HulkInfo>(root).Cause);
                        foreach (var member in f.Em.GetBuffer<LinkedEntityGroup>(root)) members.Add(member.Value);
                        DamageScenarioProbe.Request(f.Em, root, GridDissolveMode.Dissolve);
                        DamageScenarioProbe.Request(f.Em, root, GridDissolveMode.Silent);
                    }
                }
                Assert.AreEqual(600u, f.Test.GetSingleton<SimClock>().Tick - start); Assert.AreEqual((1u << 12) - 1, fired, "Every hostile ship must actually release weapon shots before dying.");
                foreach (var member in members) Assert.IsFalse(f.Em.Exists(member), "The final destroy pass owns root/LEG deletion, including overlapping duplicate requests.");
                using var requests = f.Em.CreateEntityQuery(typeof(GridDissolveRequest)); Assert.AreEqual(0, requests.CalculateEntityCount());
            }
            finally { factions.Dispose(); weapons.Dispose(); }
            LogAssert.NoUnexpectedReceived(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "R26 must release all scenario native allocations.");
            TestContext.WriteLine($"DMG-015 R26: 12 hostile ships fired {shots} actual shots; twelve scripted core losses and duplicate despawns over 600 ticks; native leaks=0.");
        }
    }
}
