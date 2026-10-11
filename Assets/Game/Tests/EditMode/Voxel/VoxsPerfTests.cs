using System;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>SPEC-05 §8 codec micro-gates; save capture and container-size gates remain PERS-012.</summary>
    public sealed class VoxsPerfTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        static void Gate(string name, Action action, double budget)
        {
            var group = new SampleGroup(name, SampleUnit.Millisecond);
            Measure.Method(action).SampleGroup(group).WarmupCount(2).MeasurementCount(10).IterationsPerMeasurement(1).Run();
            var stat = PerfRun.Gate(name, group.Samples.ToArray(), budget);
            TestContext.WriteLine($"{name}: median {stat.Median:F3} ms; p95 {stat.P95:F3} ms");
        }
        [Test, Performance, Category("Performance")]
        public void Encode_1M()
        {
            var c = VoxsCodecTests.Catalog(); using var s = VoxsTestStructures.ReferenceShip(c);
            var names = VoxsBlockNames.FromCatalog(c);
            Gate("VoxsEncode1M", () => VoxsCodec.Encode(s.Source, names, VoxsEncodeOptions.ForSaveChunk), 150);
        }
        [Test, Performance, Category("Performance")]
        public void Decode_1M()
        {
            var c = VoxsCodecTests.Catalog(); using var s = VoxsTestStructures.ReferenceShip(c);
            byte[] data = VoxsCodec.Encode(s.Source, VoxsBlockNames.FromCatalog(c), VoxsEncodeOptions.ForSaveChunk);
            Gate("VoxsDecode1M", () => {
                bool ok = VoxsCodec.TryDecode(data, c, Allocator.Persistent, out var d, out string error);
                d?.Dispose(); Assert.IsTrue(ok, error);
            }, 100);
        }
    }
}
