using System.Collections;
using System.Collections.Generic;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Entities;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;
using Fixture = Game.Tests.PlayMode.Voxel.Physics.PhysicsScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Voxel.Physics.Perf
{
    public sealed class ColliderSpawnPerfTests
    {
        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Station1M_ReadyWithin12Frames()
        {
            PerfSettings.AssertReference(); using var f = new Fixture(); var warm = f.Spawn(station: true); f.Ready(warm);
            int handle = f.Em.GetComponentData<GridSource>(warm).StructureHandle; f.Em.DestroyEntity(warm);
            using var recorder = new PhysicsMainRecorder(f);
            for (int i = 0; i < 60; i++) { f.Tick(); yield return null; }
            var samples = new double[300]; var latencies = new List<double>(); Entity root = Entity.Null; int frames = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                if (root == Entity.Null) { root = f.SpawnRegistered(handle); frames = 0; }
                samples[i] = recorder.Tick(f);
                if (!f.Em.IsComponentEnabled<GridSpawning>(root)) frames++;
                bool ready = f.Em.GetComponentData<GridColliderState>(root).Phase == GridColliderPhase.Ready;
                yield return null;
                if (frames == 13) PerfRun.WarnBudget($"Full compound not ready after {frames} frames; target <= 12 frames");
                if (ready) { latencies.Add(frames); f.Em.DestroyEntity(root); root = Entity.Null; }
            }
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("PhysicsSpawnMain", samples).AddSamples("ReadyFrames", latencies, "frames");
            Assert.Greater(latencies.Count, 10); Assert.Greater(run.Get("PhysicsSpawnMain").Median, 0);
            run.Write("PhysicsSpawnMain", budgetP95: 1); run.AssertMax("PhysicsSpawnMain", 1); run.AssertMax("ReadyFrames", 12);
            TestContext.WriteLine($"{latencies.Count} completed station spawns / 300 frames; main {run.Get("PhysicsSpawnMain").Median:F6} / {run.Get("PhysicsSpawnMain").P95:F6} ms median/p95, max {run.Get("PhysicsSpawnMain").Max:F6}; ready max {run.Get("ReadyFrames").Max} frames");
        }
    }
}
