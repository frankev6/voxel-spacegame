using Game.Core;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-002 gate (SPEC-02 §8): Hash64 of a 16-character id ≤ 0.3 µs and 0 B GC over 10,000 iterations.</summary>
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
            var perCallUs = new double[15];
            for (int round = 0; round < perCallUs.Length; round++)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                HashMany();
                perCallUs[round] = sw.Elapsed.TotalMilliseconds * 1000.0 / Iterations;
            }
            var stat = PerfRun.Gate("Hash64_PerCall", perCallUs, 0.3, "us");
            UnityEngine.Debug.Log($"StableHash.Hash64(16 chars): median {stat.Median:F4} µs per call");
        }
    }
}
