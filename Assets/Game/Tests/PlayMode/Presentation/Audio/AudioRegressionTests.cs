#if UNITY_EDITOR
using System;
using System.Collections;
using Game.App.Boot;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    /// <summary>AUD-013's allocation contract, independent of advisory timing/reference settings.</summary>
    public sealed class AudioRegressionTests
    {
        NativeLeakDetectionMode previousLeaks;
        bool previousSafety;

        [UnitySetUp] public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
            previousLeaks = NativeLeakDetection.Mode; previousSafety = BurstCompiler.Options.EnableBurstSafetyChecks;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace; BurstCompiler.Options.EnableBurstSafetyChecks = true;
        }
        [UnityTearDown] public IEnumerator Clean()
        {
            NativeLeakDetection.Mode = previousLeaks; BurstCompiler.Options.EnableBurstSafetyChecks = previousSafety;
            yield return null; LogAssert.NoUnexpectedReceived();
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
        }

        [UnityTest] public IEnumerator Combat500PerFrame_NoGcAlloc()
        {
            using var fixture = new AudioMeasurementFixture();
            var samples = new double[AudioMeasurementFixture.Samples];
            for (int frame = 0; frame < AudioMeasurementFixture.Warmup; frame++)
            {
                fixture.Prepare(500, frame); fixture.AudioFrame(500); fixture.CheckFrame(500, true);
                yield return null;
            }
            long total = 0, worst = 0;
            for (int frame = 0; frame < samples.Length; frame++)
            {
                fixture.Prepare(500, AudioMeasurementFixture.Warmup + frame);
                // Only real audio work is measured: guarded enqueue, full drain, loops, UI and service update.
                // Fixture input mutation, NUnit assertions, coroutine scheduling and JSON reporting stay outside.
                long before = GC.GetAllocatedBytesForCurrentThread(); fixture.AudioFrame(500);
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                samples[frame] = bytes; total += bytes; worst = Math.Max(worst, bytes);
                fixture.CheckFrame(500, true); yield return null;
            }
            fixture.CheckCompleted();
            const string gate = "AudioManagedBytesWorstFrame", frames = "AudioManagedBytesPerFrame";
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName)
                .AddSamples(frames, samples, "B").AddSamples(gate, new[] { (double)worst }, "B");
            // A single allocating frame must make the retained JSON fail, even when the median is zero.
            run.Write(gate, 0); run.AssertMax(frames, 0); Assert.AreEqual(0, total);
            TestContext.WriteLine($"AUD-013 GC: seed=64001, warmup=60, samples={samples.Length}, worst={worst}B, total={total}B; safety/leak checks enabled.");
        }
    }
}
#endif
