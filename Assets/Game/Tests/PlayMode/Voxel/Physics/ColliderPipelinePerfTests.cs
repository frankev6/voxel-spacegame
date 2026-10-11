using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Tests.Common.Perf;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Profiling;
using UnityEngine.TestTools;
using Fixture = Game.Tests.PlayMode.Voxel.Physics.PhysicsScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Voxel.Physics.Perf
{
    internal static class PhysicsPerfDamage
    {
        public static void Remove(Fixture f, Entity root, int3 cell)
        {
            var grid = f.Em.GetComponentData<VoxelGrid>(root);
            Entity entity = f.Em.GetBuffer<BrickSlot>(root)[BrickMath.SlotIndex(BrickMath.BrickOf(cell), grid.BrickMin, grid.BrickDims)].Brick;
            var brick = f.Em.GetComponentData<Brick>(entity); var dirty = f.Em.GetComponentData<BrickDirty>(entity);
            bool changed = BrickWrite.Set(ref brick, f.Em.GetBuffer<BrickCells>(entity), f.Em.GetBuffer<BrickSparseCells>(entity),
                f.Em.GetBuffer<BrickSolidMask>(entity), ref dirty, BrickMath.LocalIndex(BrickMath.LocalOf(cell)), default, grid.Revision + 1);
            Assert.IsTrue(changed); f.Em.SetComponentData(entity, brick); f.Em.SetComponentData(entity, dirty); f.Em.SetComponentEnabled<BrickDirty>(entity, true);
        }
        public static void Finish(Fixture f, Entity root)
        {
            var grid = f.Em.GetComponentData<VoxelGrid>(root); grid.Revision++; f.Em.SetComponentData(root, grid);
            f.Em.SetComponentEnabled<GridMassRecompute>(root, true);
        }
    }

    internal sealed class PhysicsWorkerRecorder : IDisposable
    {
        readonly ProfilerRecorder[] recorders;
        public PhysicsWorkerRecorder()
        {
            string[] jobs = { "BrickColliderJob", "GridAssembleJob", "ColliderCollectJob", "ColliderInstallJob", "ColliderFinalizeJob" };
            recorders = new ProfilerRecorder[jobs.Length];
            for (int i = 0; i < jobs.Length; i++)
            {
                recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, jobs[i] + ".Execute");
                Assert.IsTrue(recorders[i].Valid); Assert.IsTrue(recorders[i].IsRunning);
            }
        }
        public double CurrentMs { get { double ns = 0; foreach (var r in recorders) ns += r.CurrentValue; return ns * 1e-6; } }
        public double Since(double before)
        {
            if (recorders[0].Count == 0) return CurrentMs - before; // Headless: no profiler frame flush.
            double ns = 0; foreach (var r in recorders) ns += r.LastValue; return ns * 1e-6;
        }
        public void Dispose() { foreach (var r in recorders) r.Dispose(); }
    }

    public sealed class ColliderPipelinePerfTests
    {
        [BurstCompile(CompileSynchronously = true)] struct HitJob : IJob
        {
            public BrickColliderJob Bricks;
            public GridAssembleJob Assembly;
            public void Execute() { for (int i = 0; i < Bricks.Items.Length; i++) Bricks.Execute(i); Assembly.Execute(0); }
        }
        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Hit1M_ColliderJobsUnder0_5ms()
        {
            PerfSettings.AssertReference(); using var f = new Fixture(); Entity root = f.Spawn(station: true); f.Ready(root);
            var cache = f.Singleton<ColliderBuildCache>(); cache.CompleteAccess(); var record = cache.C.Grids[0]; var grid = f.Em.GetComponentData<VoxelGrid>(root);
            var changed = new List<BrickBuildItem>(); var snapshots = new List<BrickMaskBits>(); int removed = 0;
            var children = new NativeArray<BrickChild>(record.Children.Length, Allocator.Persistent);
            for (int i = 0; i < record.Children.Length; i++)
            {
                children[i] = record.Children[i]; int3 coord = children[i].Coord;
                Entity brick = f.Em.GetBuffer<BrickSlot>(root)[BrickMath.SlotIndex(coord, grid.BrickMin, grid.BrickDims)].Brick;
                var mask = new BrickMaskBits(); mask.CopyFrom(f.Em.GetBuffer<BrickSolidMask>(brick)); bool edited = false;
                for (int c = 0; c < 4096; c++)
                {
                    float3 d = (float3)BrickMath.CellOf(coord, c) + .5f - new float3(2, 16, 16);
                    if (mask.Get(c) && math.lengthsq(d) <= 9) { mask.Set(c, false); edited = true; removed++; }
                }
                if (edited) { changed.Add(new BrickBuildItem { Coord = coord, CellSize = grid.CellSize }); snapshots.Add(mask); }
            }
            Assert.AreEqual(872, children.Length); Assert.Greater(changed.Count, 0); Assert.LessOrEqual(changed.Count, 8);
            var items = new NativeArray<BrickBuildItem>(changed.ToArray(), Allocator.Persistent);
            var masks = new NativeArray<BrickMaskBits>(snapshots.ToArray(), Allocator.Persistent);
            var results = new NativeArray<BrickBuildResult>(items.Length, Allocator.Persistent);
            var assemblies = new NativeArray<AssemblyItem>(1, Allocator.Persistent);
            var apply = new NativeArray<int>(items.Length, Allocator.Persistent);
            var output = new NativeArray<AssemblyResult>(1, Allocator.Persistent);
            assemblies[0] = new AssemblyItem { CellSize = grid.CellSize, ChildCount = children.Length, ApplyCount = items.Length };
            for (int i = 0; i < apply.Length; i++) apply[i] = i;
            var job = new HitJob
            {
                Bricks = new BrickColliderJob { Items = items, Masks = masks, Results = results, MaxBoxes = 64, Bevel = .05f },
                Assembly = new GridAssembleJob { Items = assemblies, Children = children, Apply = apply, Bricks = items, BrickResults = results, Results = output },
            };
            double Sample()
            {
                long start = Stopwatch.GetTimestamp(); job.Run(); double ms = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                output[0].Compound.Dispose(); for (int i = 0; i < results.Length; i++) results[i].Blob.Dispose(); return ms;
            }
            try
            {
                for (int i = 0; i < 60; i++) { Sample(); yield return null; }
                var samples = new double[300]; for (int i = 0; i < samples.Length; i++) { samples[i] = Sample(); yield return null; }
                var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Hit1MColliderJobs", samples);
                run.Write("Hit1MColliderJobs", .5); run.AssertMedian("Hit1MColliderJobs", .5);
                TestContext.WriteLine($"Radius-3 hit: {removed} cells, {items.Length} brick builds + {children.Length}-child assembly; summed serial Burst worker work including Run overhead {run.Get("Hit1MColliderJobs").Median:F6} / {run.Get("Hit1MColliderJobs").P95:F6} ms median/p95");
            }
            finally { output.Dispose(); apply.Dispose(); assemblies.Dispose(); results.Dispose(); masks.Dispose(); items.Dispose(); children.Dispose(); }
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Skirmish30Ships_NoLateInstalls()
        {
            PerfSettings.AssertReference(); using var f = new Fixture(); var roots = new Entity[30]; var decks = new int[30][]; var remaining = new int[30];
            for (int r = 0; r < roots.Length; r++)
            {
                roots[r] = f.Spawn(position: new float3(r * 100, 0, 0)); decks[r] = new int[20000]; remaining[r] = 20000;
                for (int c = 0; c < 20000; c++) decks[r][c] = c;
            }
            f.Ready(roots); using var recorder = new PhysicsWorkerRecorder(); var random = new Unity.Mathematics.Random(0x301919u);
            using var deadline = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BrickColliderBuildSystem.Deadline");
            Assert.IsTrue(deadline.Valid); Assert.IsTrue(deadline.IsRunning);
            var worker = new double[600]; var waits = new double[600]; var urgent = new List<double>(); int assemblyCount = 0, previousBatches = GridColliderStats.Gather(f.Em).Batches;
            for (int tick = 0; tick < 660; tick++)
            {
                for (int r = 0; r < roots.Length; r++)
                {
                    for (int c = 0; c < 20; c++)
                    {
                        int pick = random.NextInt(remaining[r]), cell = decks[r][pick]; decks[r][pick] = decks[r][--remaining[r]];
                        PhysicsPerfDamage.Remove(f, roots[r], new int3(cell % 80, cell / 80 % 10, cell / 800));
                    }
                    PhysicsPerfDamage.Finish(f, roots[r]);
                }
                double before = recorder.CurrentMs; long beforeDeadline = deadline.CurrentValue; f.Tick(); yield return null;
                double ms = recorder.Since(before); var stats = GridColliderStats.Gather(f.Em);
                Assert.AreEqual(0, stats.LateInstalls, "Tick " + tick);
                if (tick >= 60)
                {
                    worker[tick - 60] = ms;
                    waits[tick - 60] = (deadline.Count == 0 ? deadline.CurrentValue - beforeDeadline : deadline.LastValue) * 1e-6;
                    if (stats.Batches != previousBatches) { urgent.Add(stats.LastBatchUrgentBricks); assemblyCount += stats.LastBatchAssemblies; }
                }
                previousBatches = stats.Batches;
            }
            for (int i = 0; i < 16; i++) { f.Tick(); yield return null; }
            Assert.AreEqual(0, GridColliderStats.Gather(f.Em).LateInstalls); foreach (int count in remaining) Assert.AreEqual(6800, count);
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("SkirmishColliderWorkers", worker)
                .AddSamples("DeadlineCompletionMain", waits).AddSamples("UrgentBricksPerBatch", urgent, "bricks");
            // A normally completed batch may need no forced deadline wait at this one-tick frame rate.
            Assert.Greater(run.Get("SkirmishColliderWorkers").Mean, 0); run.Write("SkirmishColliderWorkers", 2); run.AssertMedian("SkirmishColliderWorkers", 2);
            TestContext.WriteLine($"Variable-rate deadline completion median/p95/max {run.Get("DeadlineCompletionMain").Median:F6}/{run.Get("DeadlineCompletionMain").P95:F6}/{run.Get("DeadlineCompletionMain").Max:F6} ms; forced completions {GridColliderStats.Gather(f.Em).ForcedBatchCompletes} (ADR-0035).");
            TestContext.WriteLine($"30 ships, 60 damage warmup + 600 measured ticks, 20 distinct cells/ship/tick; no late installs. Workers median/p95/mean {run.Get("SkirmishColliderWorkers").Median:F6}/{run.Get("SkirmishColliderWorkers").P95:F6}/{run.Get("SkirmishColliderWorkers").Mean:F6} ms; urgent median/max {run.Get("UrgentBricksPerBatch").Median}/{run.Get("UrgentBricksPerBatch").Max}; assemblies/s {assemblyCount / 10.0:F1}. Mass uses real recompute; its time is excluded from the collider-job sum.");
        }
    }
}
