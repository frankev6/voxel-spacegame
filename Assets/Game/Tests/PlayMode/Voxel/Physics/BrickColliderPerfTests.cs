using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core.Systems;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.PerformanceTesting;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine.TestTools;
using Fixture = Game.Tests.PlayMode.Voxel.Physics.PhysicsScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Voxel.Physics.Perf
{
    [SetUpFixture]
    public sealed class PhysicsPerfSettingsFixture
    {
        PhysicsPerfScope scope;
        [OneTimeSetUp] public void SetUp() => scope = new PhysicsPerfScope();
        [OneTimeTearDown] public void TearDown() => scope?.Dispose();
    }

    internal sealed class PhysicsPerfScope : IDisposable
    {
        readonly bool burst = BurstCompiler.Options.EnableBurstCompilation, safety = BurstCompiler.Options.EnableBurstSafetyChecks,
            synchronous = BurstCompiler.Options.EnableBurstCompileSynchronously, debugger = JobsUtility.JobDebuggerEnabled;
        readonly NativeLeakDetectionMode leaks = NativeLeakDetection.Mode;
        public PhysicsPerfScope()
        {
            BurstCompiler.Options.EnableBurstCompilation = true; BurstCompiler.Options.EnableBurstSafetyChecks = false;
            BurstCompiler.Options.EnableBurstCompileSynchronously = true; JobsUtility.JobDebuggerEnabled = false; NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
            PerfSettings.AssertReference();
            TestContext.WriteLine($"Reference CPU settings: Burst on, safety/debugger/leaks off, {JobsUtility.JobWorkerCount} workers; {UnityEngine.SystemInfo.processorType}");
        }
        public void Dispose()
        {
            BurstCompiler.Options.EnableBurstCompilation = burst; BurstCompiler.Options.EnableBurstSafetyChecks = safety;
            BurstCompiler.Options.EnableBurstCompileSynchronously = synchronous; JobsUtility.JobDebuggerEnabled = debugger; NativeLeakDetection.Mode = leaks;
        }
    }

    /// <summary>Inclusive ECS system markers: includes cache waits and bookkeeping outside the step markers, without nested double counting.</summary>
    internal sealed class PhysicsMainRecorder : IDisposable
    {
        readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        public PhysicsMainRecorder(Fixture f)
        {
            var world = f.Test.World;
            var systems = new[] { world.GetExistingSystem<GridSpawnBodyInitSystem>(), world.GetExistingSystem<GridBodyInitSystem>(), world.GetExistingSystem<ColliderSwapSystem>(),
                world.GetExistingSystem<GridMassUpdateSystem>(), world.GetExistingSystem<ColliderSnapshotSystem>(),
                world.GetExistingSystem<BrickColliderBuildSystem>(), world.GetExistingSystem<GridColliderCleanupSystem>(),
                world.GetExistingSystemManaged<GameplayEndCommandBufferSystem>().SystemHandle,
                world.GetExistingSystemManaged<EndInitializationEntityCommandBufferSystem>().SystemHandle };
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            foreach (var system in systems)
            {
                string name = EntityManager.EntityManagerDebug.GetSystemProfilerMarkerName(world, system); int found = 0;
                foreach (var handle in handles)
                {
                    var d = ProfilerRecorderHandle.GetDescription(handle); if (d.Name != name) continue;
                    var recorder = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately);
                    Assert.IsTrue(recorder.Valid, name); Assert.IsTrue(recorder.IsRunning, name); recorders.Add(recorder); found++;
                }
                Assert.Greater(found, 0, name);
            }
        }
        public double CurrentMs { get { double ns = 0; foreach (var r in recorders) ns += r.CurrentValue; return ns * 1e-6; } }
        // -nographics may never flush a profiler frame even though UnityTest yields. Read one synchronous update's delta.
        public double Tick(Fixture f) { double before = CurrentMs; f.Tick(); return CurrentMs - before; }
        public void Dispose() { foreach (var r in recorders) r.Dispose(); }
    }

    public sealed class BrickColliderPerfTests
    {
        [BurstCompile(CompileSynchronously = true)] struct Build : IJob
        {
            public BrickColliderJob Work;
            public void Execute() => Work.Execute(0);
        }
        [UnityTest, Performance, Category("Performance")]
        public IEnumerator RebuildOneBrick_Under150us()
        {
            PerfSettings.AssertReference();
            var items = new NativeArray<BrickBuildItem>(1, Allocator.Persistent);
            var masks = new NativeArray<BrickMaskBits>(1, Allocator.Persistent);
            var results = new NativeArray<BrickBuildResult>(1, Allocator.Persistent);
            var noise = new BrickMaskBits[1000]; var random = new Unity.Mathematics.Random(0xF01819u);
            var full = new BrickMaskBits(); var slab = new BrickMaskBits();
            for (int i = 0; i < 4096; i++) { full.Set(i, true); if (BrickMath.LocalFromIndex(i).y < 4) slab.Set(i, true); }
            for (int i = 0; i < noise.Length; i++) for (int c = 0; c < 4096; c++) if (random.NextBool()) noise[i].Set(c, true);
            items[0] = new BrickBuildItem { CellSize = .5f };
            var job = new Build { Work = new BrickColliderJob { Items = items, Masks = masks, Results = results, MaxBoxes = 64, Bevel = .05f } };
            double Sample(BrickMaskBits mask)
            {
                masks[0] = mask; long start = Stopwatch.GetTimestamp(); job.Run();
                double us = (Stopwatch.GetTimestamp() - start) * 1000000.0 / Stopwatch.Frequency;
                results[0].Blob.Dispose(); return us;
            }
            try
            {
                for (int i = 0; i < 60; i++) { Sample(full); Sample(slab); Sample(noise[i]); yield return null; }
                var fullTimes = new double[300]; var slabTimes = new double[300]; var noiseTimes = new double[1000]; int n = 0;
                for (int frame = 0; frame < 300; frame++)
                {
                    fullTimes[frame] = Sample(full); slabTimes[frame] = Sample(slab);
                    for (int i = 0; i < (frame < 100 ? 4 : 3); i++) { noiseTimes[n] = Sample(noise[n]); n++; }
                    yield return null;
                }
                var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("Noise50Brick", noiseTimes, "us")
                    .AddSamples("FullBrick", fullTimes, "us").AddSamples("SlabBrick", slabTimes, "us");
                run.Write("Noise50Brick", budgetP95: 150); run.AssertP95("Noise50Brick", 150);
                run.AssertP95("FullBrick", 20); run.AssertP95("SlabBrick", 20);
                foreach (string name in new[] { "Noise50Brick", "FullBrick", "SlabBrick" })
                { var s = run.Get(name); TestContext.WriteLine($"{name}: median {s.Median:F3}, p95 {s.P95:F3} us ({s.Samples} samples)"); }
            }
            finally { items.Dispose(); masks.Dispose(); results.Dispose(); }
        }
    }
}
