using System;
using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Simulation.Weapons;
using Game.Simulation.Spatial;
using Game.Simulation.Power;
using Game.Tests.Common;
using Game.Voxel.Damage;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using FlightFixture = Game.Tests.PlayMode.Simulation.Flight.FlightScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Combat
{
    internal struct SkirmishScript : IComponentData
    {
        public Entity A, B, C, D;
        public uint Start;
        public byte ChunkSpur;
    }

    // Scripted SPEC-21 producer: fixed-tick hits enter the real damage channel; no direct cell edits.
    [DisableAutoCreation, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(ImpactEventSystem)), UpdateBefore(typeof(DamageResolveSystem))]
    internal partial struct SkirmishShotsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        { state.RequireForUpdate<SkirmishScript>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<SimClock>(); }
        public void OnUpdate(ref SystemState state)
        {
            var streams = SystemAPI.GetSingleton<DamageStreams>();
            var writer = streams.Damage.BeginWrite(ref state, ProducerKind.Projectile, 1);
            state.Dependency = new ShotsJob { Script = SystemAPI.GetSingleton<SkirmishScript>(),
                Tick = SystemAPI.GetSingleton<SimClock>().Tick, Writer = writer }.Schedule(state.Dependency);
            streams.Damage.EndWrite(state.Dependency);
        }
        [BurstCompile] struct ShotsJob : IJob
        {
            public SkirmishScript Script;
            public uint Tick;
            public NativeStream.Writer Writer;
            public void Execute()
            {
                Writer.BeginForEachIndex(0);
                uint t = Tick - Script.Start;
                if (t < 600 && t % 15 == 0)
                {
                    Fire(Script.A, Script.D, new float3(.25f), 1, t * 4);
                    Fire(Script.B, Script.A, new float3(.75f,.25f,.25f), 1, t * 4 + 1);
                    Fire(Script.C, Script.B, new float3(1.25f,.25f,.25f), 1, t * 4 + 2);
                    Fire(Script.D, Script.C, new float3(1.75f,.25f,.25f), 1, t * 4 + 3);
                }
                if (t == 120) Fire(Script.D, Script.A, new float3(5.75f,.25f,1.25f), 100000, 3000);
                if (t == 300) Fire(Script.A, Script.D, new float3(1.25f,.75f,1.25f), 100000, 3001);
                if (Script.ChunkSpur != 0 && t == 180) Fire(Script.D, Script.A, new float3(4.25f,.25f,2.75f), 100000, 3002);
                Writer.EndForEachIndex();
            }
            void Fire(Entity source, Entity target, float3 point, float amount, uint sequence)
                => Writer.Write(new DamageEvent { Grid = target, Source = source, Instigator = source,
                    LocalPoint = point, LocalDir = new float3(0,1,0), Amount = amount,
                    Type = DamageType.Kinetic, Producer = ProducerKind.Projectile, Sequence = sequence });
        }
    }

    /// <summary>DMG-014's three required checks, with production spawn, collision, damage, integrity, split and death systems.</summary>
    [Category("Scenario")]
    public unsafe sealed class DamageDeterminismTests
    {
        internal sealed class Fixture : IDisposable
        {
            public readonly FlightFixture Flight;
            public readonly Entity[] Roots = new Entity[4];
            public EntityManager Em => Flight.Em;
            readonly bool chunkSpur;
            public Fixture(bool chunkSpur = false)
            {
                this.chunkSpur = chunkSpur;
                var systems = new List<Type> { typeof(DamageStreamsSystem), typeof(ImpactEventSystem), typeof(DamageResolveSystem),
                    typeof(IntegritySystem), typeof(SplitSystem), typeof(ShipDeathSystem), typeof(ChunkColliderCleanupSystem),
                    typeof(FxEventQueueCreateSystem), typeof(SfxEventQueueCreateSystem), typeof(SpatialHashBuildSystem), typeof(ShieldSystem), typeof(ExplosionSystem), typeof(SkirmishShotsSystem) };
                if (chunkSpur) systems.Add(typeof(SplitGraceContactSystem));
                Flight = new FlightFixture(systems.ToArray());
                Em.CreateSingleton(new PersistentIdAllocator { Next = 1000, End = 2000 });
                Em.CreateSingleton(new WeaponTuning { MaxCandidates = 32, ExplosionImpulsePerDamage = 2 });
                for (int i = 0; i < 4; i++)
                {
                    Roots[i] = Flight.SpawnSource(Build(Flight, chunkSpur && i == 0), new float3(i * 200,0,0));
                }
                Flight.Ready(Roots);
                // Asynchronous spawn readiness can finish on different ticks. Begin each comparison
                // at the same absolute clock so collider install buckets and lifetimes have identical inputs.
                Assert.Less(Flight.Test.TicksRun, 120u);
                while (Flight.Test.TicksRun < 120) Flight.Tick();
                for (int i = 0; i < 4; i++) Em.SetComponentData(Roots[i], new PersistentId { Value = (ulong)(100 + i) });
            }
            internal static VoxelStructure Build(FlightFixture flight, bool chunkSpur = false)
            {
                var s = new VoxelStructure(.5f);
                // Test-only connected hull: two lobes joined by one cell, core and real weapon block.
                for (int x = 0; x <= 17; x++) for (int z = 0; z <= 4; z++)
                    if (x != 11 || z == 2) Place(flight, s, "block_hull", new int3(x,0,z));
                Place(flight, s, "block_core_small", new int3(2,1,2));
                Place(flight, s, "weapon_plasma_turret_small", new int3(14,1,2));
                if (chunkSpur)
                {
                    Place(flight, s, "block_hull", new int3(8,0,5));
                    for (int x = 7; x <= 9; x++) for (int z = 6; z <= 9; z++) Place(flight, s, "block_hull", new int3(x,0,z));
                }
                return s;
            }
            static void Place(FlightFixture flight, VoxelStructure source, string id, int3 origin)
                => Assert.AreEqual(PlaceResult.Ok, source.TryPlaceBlock(new BlockPlacement { Block = flight.Index(id), Origin = origin }, flight.Content), id);
            public void Begin()
            {
                // Physical ram, after every collider is ready. Preparation does not consume scripted combat ticks.
                Em.SetComponentData(Roots[2], LocalTransform.FromPosition(new float3(220,0,0)));
                Em.SetComponentData(Roots[1], new PhysicsVelocity { Linear = new float3(15,0,0) });
                Em.SetComponentData(Roots[2], new PhysicsVelocity { Linear = new float3(-15,0,0) });
                uint tick = Flight.Test.GetSingleton<SimClock>().Tick;
                Em.CreateSingleton(new SkirmishScript { A = Roots[0], B = Roots[1], C = Roots[2], D = Roots[3], Start = tick, ChunkSpur = (byte)(chunkSpur ? 1 : 0) });
            }
            // DMG-015 repeats the same fixture in one World; old grids leave through the production death path.
            public void Respawn()
            {
                using (var query = Em.CreateEntityQuery(typeof(VoxelGrid)))
                using (var grids = query.ToEntityArray(Allocator.Temp))
                    foreach (var grid in grids) DamageScenarioProbe.Request(Em, grid, GridDissolveMode.Dissolve);
                for (int i = 0; i < Roots.Length; i++)
                    Roots[i] = Flight.SpawnSource(Build(Flight, chunkSpur && i == 0), new float3(i == 2 ? 220 : i * 200,0,0),
                        new float3(i == 1 ? 15 : i == 2 ? -15 : 0,0,0));
                // New roots pass through normal asynchronous readiness while every soak tick remains observed.
                using var script = Em.CreateEntityQuery(typeof(SkirmishScript));
                script.SetSingleton(new SkirmishScript { A = Roots[0], B = Roots[1], C = Roots[2], D = Roots[3],
                    Start = Flight.Test.GetSingleton<SimClock>().Tick, ChunkSpur = (byte)(chunkSpur ? 1 : 0) });
            }
            public void Dispose() => Flight.Dispose();
        }
        static ulong Run(int fps, int workers)
        {
            int previous = JobsUtility.JobWorkerCount; JobsUtility.JobWorkerCount = workers;
            try
            {
                using var f = new Fixture(); f.Begin(); uint start = f.Flight.Test.TicksRun;
                int frame = 0;
                while (f.Flight.Test.TicksRun - start < 600)
                {
                    f.Flight.Test.StepFrame(1f / fps);
                    Assert.Less(++frame, fps * 11, "The 600-tick scenario must finish within eleven simulated seconds.");
                }
                Assert.AreEqual(600u, f.Flight.Test.TicksRun - start);
                var streams = f.Flight.Test.GetSingleton<DamageStreams>(); streams.Data->AllJobs(default).Complete();
                Assert.Greater(streams.Data->RamPairs.Count, 0, "The scenario must produce actual physics ram damage.");
                Assert.IsTrue(f.Em.HasComponent<Hulk>(f.Roots[3]), "The scripted core kill must pass through ShipDeathSystem.");
                Assert.AreEqual(HulkCause.CoreLost, f.Em.GetComponentData<HulkInfo>(f.Roots[3]).Cause);
                using var hulks = f.Em.CreateEntityQuery(typeof(HulkInfo));
                bool split = false;
                using var hulkInfos = hulks.ToComponentDataArray<HulkInfo>(Allocator.Temp);
                foreach (var info in hulkInfos) split |= info.Cause == HulkCause.Split;
                Assert.IsTrue(split, "The bridge cut must produce a physical split hulk containing its weapon.");
                using (var grids = f.Em.CreateEntityQuery(typeof(VoxelGrid), typeof(PersistentId)))
                using (var roots = grids.ToEntityArray(Allocator.Temp))
                    foreach (var root in roots)
                        TestContext.WriteLine($"DMG-014 grid: fps={fps}, workers={workers}, id={f.Em.GetComponentData<PersistentId>(root).Value}, entity={root.Index}:{root.Version}, hash={CellStateHasher.ComputeGridHash(f.Em, root):X16}");
                ulong hash = CellStateHasher.ComputeWorldHash(f.Em);
                TestContext.WriteLine($"DMG-014: fps={fps}, workers={workers}, ticks=600, hash={hash:X16}");
                return hash;
            }
            finally { JobsUtility.JobWorkerCount = previous; }
        }
        [Test] public void Hash_IdenticalAt30_60_144Fps()
        {
            ulong expected = Run(60, 1);
            Assert.AreEqual(expected, Run(30, 1)); Assert.AreEqual(expected, Run(144, 1));
        }
        [Test] public void Hash_IdenticalAcrossWorkerCounts()
        {
            ulong expected = Run(60, 1);
            foreach (int fps in new[] { 30, 60, 144 }) Assert.AreEqual(expected, Run(fps, JobsUtility.JobWorkerMaximumCount));
        }
        [Test] public void Hash_SensitiveToOneQuantum()
        {
            using var f = new Fixture(); Entity root = f.Roots[0]; ulong before = CellStateHasher.ComputeWorldHash(f.Em);
            var grid = f.Em.GetComponentData<VoxelGrid>(root); int3 pos = new int3(0);
            Entity brickEntity = f.Em.GetBuffer<BrickSlot>(root)[BrickMath.SlotIndex(BrickMath.BrickOf(pos), grid.BrickMin, grid.BrickDims)].Brick;
            var dense = f.Em.GetBuffer<BrickCells>(brickEntity); var sparse = f.Em.GetBuffer<BrickSparseCells>(brickEntity);
            int local = BrickMath.LocalIndex(BrickMath.LocalOf(pos)); var cell = BrickWrite.Get(dense, sparse, local); cell.Hp--;
            var brick = f.Em.GetComponentData<Brick>(brickEntity); var dirty = f.Em.GetComponentData<BrickDirty>(brickEntity);
            Assert.IsTrue(BrickWrite.Set(ref brick, dense, sparse, f.Em.GetBuffer<BrickSolidMask>(brickEntity), ref dirty, local, cell, grid.Revision + 1));
            f.Em.SetComponentData(brickEntity, brick); f.Em.SetComponentData(brickEntity, dirty);
            Assert.AreNotEqual(before, CellStateHasher.ComputeWorldHash(f.Em));
        }
    }
}
