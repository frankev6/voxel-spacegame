using System;
using System.Diagnostics;
using Game.App.DevRuntime;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.Profiling;

namespace Game.Tests.EditMode.App
{
    public sealed class DevHostPerfTests
    {
        [Test, Performance, Category("Performance")]
        public void Idle_Smoke()
        {
            var host = DevRuntimeHost.Create(null);
            try
            {
                for (int i = 0; i < 100; i++) host.Update();
                var times = new double[31];
                using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 10000,
                    ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                for (int sample = 0; sample < times.Length; sample++)
                {
                    long start = Stopwatch.GetTimestamp();
                    for (int i = 0; i < 1000; i++) host.Update();
                    times[sample] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / 1000;
                }
                allocations.Stop();
                Assert.AreEqual(0, allocations.Count, "idle host must produce no GC allocation markers");
                var stats = PerfRun.Gate("DevHostIdle", times, .02);
                TestContext.WriteLine($"Idle host median {stats.Median:F6} ms, 0 GC allocation markers");
            }
            finally { host.Dispose(); }
        }
    }
}
