using System.Collections;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;
using Fixture = Game.Tests.PlayMode.Voxel.Physics.PhysicsScenarioTests.Fixture;

namespace Game.Tests.PlayMode.Voxel.Physics.Perf
{
    public sealed class PhysicsIdlePerfTests
    {
        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Station1M_IdleUnder0_1ms()
        {
            PerfSettings.AssertReference(); using var f = new Fixture(); var root = f.Spawn(station: true); f.Ready(root);
            using var recorder = new PhysicsMainRecorder(f);
            for (int i = 0; i < 60; i++) { f.Tick(); yield return null; }
            var samples = new double[300];
            for (int i = 0; i < samples.Length; i++) { samples[i] = recorder.Tick(f); yield return null; }
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("PhysicsIdle", samples);
            Assert.Greater(run.Get("PhysicsIdle").Median, 0); run.Write("PhysicsIdle", .1); run.AssertMedian("PhysicsIdle", .1);
            TestContext.WriteLine($"Inclusive PHY systems + gameplay ECB: {run.Get("PhysicsIdle").Median:F6} / {run.Get("PhysicsIdle").P95:F6} ms median/p95; 60 warmup + 300 frames");
        }
    }
}
