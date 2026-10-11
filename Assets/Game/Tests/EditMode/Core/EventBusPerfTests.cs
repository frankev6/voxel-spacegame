using System;
using Game.Core.Events;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-006 gate (SPEC-02 §8): 1,000 events × 3 handlers publish and drain in ≤ 0.3 ms.</summary>
    public sealed class EventBusPerfTests
    {
        struct EvPerf { public int V; }

        [SetUp] public void SetUp() => EventHub.Reset();
        [TearDown] public void TearDown() => EventHub.Reset();

        [Test, Performance, Category("Performance")]
        public void Drain1000()
        {
            int sum = 0;
            EventBus<EvPerf>.Subscribe(e => sum += e.V); EventBus<EvPerf>.Subscribe(e => sum -= e.V); EventBus<EvPerf>.Subscribe(e => sum ^= e.V);
            void Round() { for (int i = 0; i < 1000; i++) EventBus<EvPerf>.Publish(new EvPerf { V = i }); EventHub.DrainAll(); }
            for (int i = 0; i < 20; i++) Round();
            Measure.Method(Round).WarmupCount(5).MeasurementCount(30).SampleGroup(new SampleGroup("Drain1000", SampleUnit.Millisecond)).Run();
            var times = new double[31];
            for (int r = 0; r < times.Length; r++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew(); Round(); times[r] = sw.Elapsed.TotalMilliseconds;
            }
            var stat = PerfRun.Gate("Drain1000", times, 0.3);
            UnityEngine.Debug.Log($"EventBus 1000 events x 3 handlers: median {stat.Median:F4} ms");
        }
    }
}
