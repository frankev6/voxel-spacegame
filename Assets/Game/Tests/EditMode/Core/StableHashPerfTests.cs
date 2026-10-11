using Game.Core;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-002 gate (SPEC-02 §8): Hash64 of a 16-character id ≤ 0.3 µs and 0 B GC over 10,000 iterations.
    /// TST-001 moves the measurement onto PerfRun/PerfSettings when the test kit exists.</summary>
    public sealed class StableHashPerfTests
    {
        const string Id16 = "block_thruster_m";   // 16 characters
        const int Iterations = 10000;

        static ulong HashMany()
        {
            ulong acc = 0;
            for (int i = 0; i < Iterations; i++) acc ^= StableHash.Hash64(Id16);
            return acc;
        }

        [Test, Performance, Category("Performance")]
        public void Hash64_ShortString()
        {
            Assert.AreEqual(16, Id16.Length);
            HashMany();   // warm up (JIT, encoders)
            Assert.That(() => { HashMany(); }, Is.Not.AllocatingGCMemory(), "0 B GC");
            Measure.Method(() => HashMany()).WarmupCount(3).MeasurementCount(15).SampleGroup(new SampleGroup("Hash64_x10000", SampleUnit.Microsecond)).Run();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int round = 0; round < 5; round++) HashMany();
            double perCallUs = sw.Elapsed.TotalMilliseconds * 1000.0 / (5.0 * Iterations);
            UnityEngine.Debug.Log($"StableHash.Hash64(16 chars): {perCallUs:F4} µs per call");
            Assert.LessOrEqual(perCallUs, 0.3, "≤ 0.3 µs per call");
        }
    }
}
