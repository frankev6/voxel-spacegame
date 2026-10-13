using System.Collections.Generic;
using Game.Core;
using Game.Core.Components;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Damage
{
    public unsafe sealed class SplitPlanTests
    {
        sealed class Fixture : System.IDisposable
        {
            public readonly TestGridFactory F = new TestGridFactory();
            public readonly SystemHandle Owner, Resolver, Integrity;
            public readonly Entity Clock, AllocatorEntity;
            public DamageStreams Streams;
            public NativeList<IslandRecord> Bodies = new NativeList<IslandRecord>(Allocator.Persistent);
            public NativeQueue<FxEvent> Fx = new NativeQueue<FxEvent>(Allocator.Persistent);
            public NativeQueue<SfxEvent> Sfx = new NativeQueue<SfxEvent>(Allocator.Persistent);
            public Fixture()
            {
                Owner = F.World.CreateSystem<DamageStreamsSystem>(); Resolver = F.World.CreateSystem<DamageResolveSystem>(); Integrity = F.World.CreateSystem<IntegritySystem>();
                Clock = F.EM.CreateSingleton(new SimClock()); AllocatorEntity = F.EM.CreateSingleton(new PersistentIdAllocator { Next = 10, End = 100 });
                using var q = F.EM.CreateEntityQuery(typeof(DamageStreams)); Streams = q.GetSingleton<DamageStreams>();
            }
            public DamageConfig Config
            {
                get { using var q = F.EM.CreateEntityQuery(typeof(DamageConfig)); return q.GetSingleton<DamageConfig>(); }
                set { using var q = F.EM.CreateEntityQuery(typeof(DamageConfig)); q.SetSingleton(value); }
            }
            public void Begin(uint tick)
            { F.EM.SetComponentData(Clock, new SimClock { Tick = tick }); Owner.Update(F.World.Unmanaged); Complete(); }
            public void Resolve() { Resolver.Update(F.World.Unmanaged); Complete(); }
            public void Find() { Integrity.Update(F.World.Unmanaged); Complete(); }
            public void Complete() { F.EM.CompleteAllTrackedJobs(); Streams.Data->AllJobs(default).Complete(); }
            public void Plan()
            {
                ref var state = ref F.World.Unmanaged.ResolveSystemStateRef(Owner);
                new PlanSplitJob { Islands = Streams.Data->Islands, Masks = Streams.Data->IslandMasks, Reader = GridCellReader.Create(ref state),
                    Bricks = state.GetComponentLookup<Brick>(true), Seeds = state.GetBufferLookup<IntegritySeed>(), Allocators = state.GetComponentLookup<PersistentIdAllocator>(true),
                    AllocatorEntity = AllocatorEntity, HulkAvailable = false, Config = Config, Bodies = Bodies, Detach = Streams.Data->PendingDetachNext, Data = Streams.Data }.Execute();
            }
            public void Emit()
            {
                ref var state = ref F.World.Unmanaged.ResolveSystemStateRef(Owner);
                new EmitDebrisJob { Removed = Streams.Data->Removed, Ranges = Streams.Data->RemovedGrids, Grids = state.GetComponentLookup<VoxelGrid>(true),
                    Blocks = F.Content.Service.BlockDefs, Config = Config, Fx = Fx.AsParallelWriter(), Sfx = Sfx.AsParallelWriter(), Data = Streams.Data }.Execute();
            }
            public bool Solid(Entity root, int x)
            { using var writer = F.Writer(root); return writer.Access.TryGetCell(new int3(x,0,0), out _, out _, out _); }
            public void Dispose() { Complete(); Bodies.Dispose(); Fx.Dispose(); Sfx.Dispose(); F.Dispose(); }
        }
        [Test] public void ParticleIsland_DetachedNextTick()
        {
            using var f = new Fixture(); var source = f.F.Box(0, new int3(15,0,0)); source.SetCell(new int3(10,0,0), default); var root = f.F.Spawn(source);
            f.F.EM.AddComponentData(root, new PhysicsVelocity { Linear = new float3(7,0,2) });
            f.F.EM.AddComponentData(root, new PhysicsMass { Transform = RigidTransform.identity, InverseMass = 1 });
            f.F.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(9,0,0) });
            f.F.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(11,0,0) });
            f.Begin(10); f.Resolve(); f.Find(); f.Emit(); f.Plan();
            Assert.AreEqual(1, f.Streams.Data->Current.ParticleIslands); Assert.AreEqual(0, f.Fx.Count);
            for (int x = 11; x <= 15; x++) Assert.IsTrue(f.Solid(root, x));
            f.Begin(11); f.Resolve(); f.Emit(); Assert.AreEqual(5, f.Fx.Count); Assert.AreEqual(5, f.Streams.Data->Removed.Length);
            foreach (var removed in f.Streams.Data->Removed)
            {
                Assert.AreEqual(CellRemovedReason.DetachedParticle, removed.Reason); Assert.AreEqual(new float3(7,0,2), removed.WorldVel);
                var fx = f.Fx.Dequeue(); Assert.AreEqual(Game.Data.Definitions.FxKind.Debris, fx.Kind);
                Assert.AreEqual(removed.WorldVel + removed.Kick, fx.Vel); Assert.AreEqual(FxEventFlags.PaletteColor, fx.Flags);
            }
            for (int x = 11; x <= 15; x++) Assert.IsFalse(f.Solid(root, x));
        }
        [Test] public void TierBoundaries()
        {
            var c = DamageConfig.Default;
            foreach (var pair in new[] { (8, SplitTier.Particle), (9, SplitTier.Chunk), (500, SplitTier.Chunk), (501, SplitTier.Hulk) })
                Assert.AreEqual(pair.Item2, PlanSplitJob.Tier(new IslandRecord { CellCount = pair.Item1, CellMax = new int3(7) }, c));
            Assert.AreEqual(SplitTier.Hulk, PlanSplitJob.Tier(new IslandRecord { CellCount = 3, CellMax = new int3(2), HasFunctional = 1 }, c));
            Assert.AreEqual(SplitTier.Hulk, PlanSplitJob.Tier(new IslandRecord { CellCount = 300, CellMax = new int3(16,4,4) }, c));
        }
        [Test] public void DebrisBudget_Subsamples()
        {
            using var f = new Fixture(); var root = f.F.Spawn(f.F.Box(0, 0)); var c = f.Config; c.CueCellBreak = new ContentHash(31); f.Config = c;
            ushort block = f.F.Content.Index("block_hull");
            for (int i = 0; i < 5000; i++) f.Streams.Data->Removed.Add(new CellRemoved { Grid = root, Reason = CellRemovedReason.Destroyed,
                Block = block, Color = 3, WorldPos = new float3(i,0,0), WorldVel = new float3(2,0,0), Kick = new float3(0,0,1) });
            f.Streams.Data->RemovedGrids.Add(new GridRemovedRange { Grid = root, Start = 0, Count = 5000 }); f.Emit();
            Assert.AreEqual(1667, f.Fx.Count); Assert.LessOrEqual(f.Fx.Count, 2000);
            int index = 0; while (f.Fx.TryDequeue(out var fx))
            {
                Assert.AreEqual(index++ * 3, fx.Pos.x); Assert.That(fx.Scale, Is.EqualTo(.5f * math.pow(3, 1f/3)).Within(1e-6f));
                Assert.AreEqual(new float3(2,0,1), fx.Vel); Assert.AreEqual(3u | ((uint)f.F.Content.Service.BlockDefs.Value.Blocks[block].MaterialLayer << 8), fx.Color);
            }
            Assert.AreEqual(1, f.Sfx.Count); var sound = f.Sfx.Dequeue(); Assert.AreEqual(c.CueCellBreak, sound.Cue); Assert.AreEqual(1, sound.Volume); Assert.AreEqual(float3.zero, sound.Pos);
            TestContext.WriteLine("5000 removed cells -> 1667 debris events, stride 3, cube-root scale; one break cue.");
        }
        [Test] public void SplitBudget_DefersDeterministically()
        {
            int original = JobsUtility.JobWorkerCount; List<int3> baseline = null;
            try
            {
                foreach (int workers in new[] { 1, JobsUtility.JobWorkerMaximumCount })
                {
                    JobsUtility.JobWorkerCount = workers;
                    using var f = new Fixture(); var c = f.Config; c.SplitCellBudgetPerTick = 5; f.Config = c;
                    var source = f.F.Box(0, new int3(31,0,0)); source.SetCell(new int3(20,0,0), default); source.SetCell(new int3(26,0,0), default);
                    var root = f.F.Spawn(source);
                    foreach (int x in new[] { 19,21,27 }) f.F.EM.GetBuffer<IntegritySeed>(root).Add(new IntegritySeed { Cell = new int3(x,0,0) });
                    var order = new List<int3>();
                    f.Begin(1); f.Resolve(); f.Find(); f.Plan(); Assert.AreEqual(1, f.Streams.Data->Current.SplitsDeferred);
                    Assert.GreaterOrEqual(f.F.EM.GetBuffer<IntegritySeed>(root).Length, 2, "Deferred island plus retained anchor");
                    for (uint tick = 2; tick <= 3; tick++)
                    {
                        f.Begin(tick); f.Resolve(); Assert.AreEqual(5, f.Streams.Data->Removed.Length);
                        foreach (var removed in f.Streams.Data->Removed) order.Add(removed.Cell);
                        f.Find(); f.Plan();
                    }
                    Assert.AreEqual(10, order.Count); if (baseline == null) baseline = order; else CollectionAssert.AreEqual(baseline, order);
                    for (int x = 0; x < 20; x++) Assert.IsTrue(f.Solid(root, x));
                    TestContext.WriteLine($"workers={workers}: two 5-cell islands detached over two ticks, ordered identically.");
                }
            }
            finally { JobsUtility.JobWorkerCount = original; }
        }
    }
}
