using System;
using Game.Core.Save;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-007 gate (SPEC-02 §8): 1 MiB of mixed primitives in ≤ 3 ms.</summary>
    public sealed class SaveWriterPerfTests
    {
        static int WriteMiB()
        {
            using var w = new SaveWriter();
            int i = 0;
            while (w.Length < 1024 * 1024)
            {
                w.WriteInt32(i); w.WriteSingle(i * 0.5f); w.WriteVarUInt32((uint)i); w.WriteBool((i & 1) == 0); w.WriteDouble(i); w.WriteString("block_hull");
                i++;
            }
            return w.Length;
        }

        [Test, Performance, Category("Performance")]
        public void Write1MiB()
        {
            for (int k = 0; k < 5; k++) WriteMiB();
            Measure.Method(() => WriteMiB()).WarmupCount(3).MeasurementCount(15).SampleGroup(new SampleGroup("Write1MiB", SampleUnit.Millisecond)).Run();
            var t = new double[15];
            for (int k = 0; k < t.Length; k++) { var sw = System.Diagnostics.Stopwatch.StartNew(); WriteMiB(); t[k] = sw.Elapsed.TotalMilliseconds; }
            var stat = PerfRun.Gate("Write1MiB", t, 3.0);
            UnityEngine.Debug.Log($"SaveWriter 1 MiB mixed: median {stat.Median:F3} ms");
        }
    }
}
