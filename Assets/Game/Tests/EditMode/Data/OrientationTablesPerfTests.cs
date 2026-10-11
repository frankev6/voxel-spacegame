using System;
using Game.Data.Blocks;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-003 gate: generation ≤ 1 ms (median of 20 runs). TST-001 moves this onto PerfRun.</summary>
    public sealed class OrientationTablesPerfTests
    {
        [Test, Performance, Category("Performance")]
        public void Generate()
        {
            for (int i = 0; i < 5; i++) OrientationTableGenerator.Generate();
            Measure.Method(() => OrientationTableGenerator.Generate()).WarmupCount(3).MeasurementCount(20).SampleGroup(new SampleGroup("Generate", SampleUnit.Millisecond)).Run();
            var t = new double[20];
            for (int i = 0; i < t.Length; i++) { var sw = System.Diagnostics.Stopwatch.StartNew(); OrientationTableGenerator.Generate(); t[i] = sw.Elapsed.TotalMilliseconds; }
            Array.Sort(t);
            UnityEngine.Debug.Log($"Orientation tables: median {t[10]:F3} ms");
            Assert.LessOrEqual(t[10], 1.0);
        }
    }
}
