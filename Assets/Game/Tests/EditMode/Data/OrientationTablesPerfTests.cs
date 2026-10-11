using System;
using Game.Data.Blocks;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-003 gate: generation ≤ 1 ms (median of 20 runs).</summary>
    public sealed class OrientationTablesPerfTests
    {
        [Test, Performance, Category("Performance")]
        public void Generate()
        {
            for (int i = 0; i < 5; i++) OrientationTableGenerator.Generate();
            Measure.Method(() => OrientationTableGenerator.Generate()).WarmupCount(3).MeasurementCount(20).SampleGroup(new SampleGroup("Generate", SampleUnit.Millisecond)).Run();
            var t = new double[20];
            for (int i = 0; i < t.Length; i++) { var sw = System.Diagnostics.Stopwatch.StartNew(); OrientationTableGenerator.Generate(); t[i] = sw.Elapsed.TotalMilliseconds; }
            var stat = PerfRun.Gate("Generate", t, 1.0);
            UnityEngine.Debug.Log($"Orientation tables: median {stat.Median:F3} ms");
        }
    }
}
