using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Game.Core.Save;
using Game.Persistence.Container;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>SPEC-05 §8/PERS-012: exact compressed FLET payload bytes, including deferred-block framing.</summary>
    public sealed class VoxsSizeTests
    {
        [TearDown] public void TearDown() => TestContentFactory.DestroyAll();
        static int Size(bool damaged)
        {
            var c = VoxsCodecTests.Catalog(); using var s = VoxsTestStructures.ReferenceShip(c, damaged);
            using var w = new SaveWriter(); w.WriteByteArray(VoxsCodec.Encode(s.Source, VoxsBlockNames.FromCatalog(c), VoxsEncodeOptions.ForSaveChunk));
            var chunks = new List<RawChunk> {
                new RawChunk { Key="META",Version=1,Payload=new byte[]{123,125} },
                new RawChunk { Key="IDPL",Version=1,Payload=new byte[]{0} },
                new RawChunk { Key="FLET",Version=1,Payload=w.ToArray() } };
            byte[] container = SaveContainerWriter.Write(default, chunks, 64);
            int entry = ContainerHeader.Size + 2 * SaveContainerWriter.TableEntrySize;
            Assert.AreEqual(1, BinaryPrimitives.ReadUInt16LittleEndian(container.AsSpan(entry + 6)), "Deflated");
            return (int)BinaryPrimitives.ReadUInt32LittleEndian(container.AsSpan(entry + 8));
        }
        static double[] Samples(bool damaged)
        {
            var sizes = new double[10];
            for (int i = -2; i < sizes.Length; i++)
            {
                int bytes = Size(damaged);
                if (i >= 0) sizes[i] = bytes;
            }
            return sizes;
        }
        [Test, Performance, Category("Performance")]
        public void ReferenceShip_1M_Under80KB()
        {
            var stat = PerfRun.Gate("ReferenceShipBytes", Samples(false), 80 * 1024, "bytes");
            TestContext.WriteLine($"Reference 1M-cell ship: {stat.Median} compressed bytes");
        }
        [Test, Performance, Category("Performance")]
        public void ReferenceShip_Damaged_Report()
        {
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("DamagedShipBytes", Samples(true), "bytes");
            run.Write();
            TestContext.WriteLine($"Reference ship with 1% damaged cells: {run.Get("DamagedShipBytes").Median} compressed bytes (report only)");
        }
    }
}
