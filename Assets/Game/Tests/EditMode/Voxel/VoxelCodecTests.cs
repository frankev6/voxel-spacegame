using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-009: the structure adapter shares SPEC-05 bytes, ownership and content-drift semantics.</summary>
    public sealed class VoxelCodecTests
    {
        static VoxelStructure Edited(VoxelTestContent fixture)
        {
            var structure = new VoxelStructure(); var content = fixture.Content; var rng = new Random(7);
            var types = new[] { fixture.Index("block_hull"), fixture.Index("block_hull_angled"), fixture.Index("block_thruster_medium"), fixture.Index("block_core_cross") };
            for (int i = 0; i < 5000; i++)
            {
                int3 origin = rng.NextInt3(-4, 5) * 4;
                if (rng.NextInt(3) == 0) structure.TryRemoveBlockAt(origin, content, out _);
                else
                {
                    var p = new BlockPlacement { Block = types[rng.NextInt(types.Length)], Origin = origin, Orient = (byte)rng.NextInt(16), Color = (byte)rng.NextInt(8) };
                    if (structure.TryPlaceBlock(p, content) == PlaceResult.Ok)
                    { var cell = structure.GetCell(origin); cell.Hp = (byte)rng.NextInt(1, 255); cell.Flags |= (byte)CellFlags.Scorched; structure.SetCell(origin, cell); }
                }
            }
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(new BlockPlacement { Block = types[3], Origin = new int3(1000 + i * 5, 0, 0) }, content));
            for (int i = 0; i < 10; i += 2) structure.TryRemoveBlockAt(new int3(1000 + i * 5, 0, 0), content, out _);
            Assert.Greater(structure.Data.FreeInstances.Length, 0);
            return structure;
        }
        static VoxelStructure Scenario(int index, VoxelTestContent fixture) => index switch
        {
            0 => new VoxelStructure(),
            1 => BenchmarkStructures.Ship20k(fixture.Index("block_hull")),
            2 => BenchmarkStructures.HollowStation1M(fixture.Index("block_hull")),
            3 => BenchmarkStructures.SolidAsteroid1M(fixture.Index("block_hull")),
            _ => Edited(fixture)
        };
        [Test]
        public void RoundTrip_ByteIdentical()
        {
            using var fixture = new VoxelTestContent(); var names = VoxsBlockNames.FromCatalog(fixture.Service.Catalog);
            for (int scenario = 0; scenario < 5; scenario++)
            {
                using var structure = Scenario(scenario, fixture);
                foreach (var options in new[] { VoxsEncodeOptions.ForAsset, VoxsEncodeOptions.ForSaveChunk })
                {
                    byte[] first = VoxelStructureCodec.Encode(structure, names, options);
                    Assert.IsTrue(VoxelStructureCodec.TryDecode(first, fixture.Service.Catalog, out var decoded, out string error), error);
                    using (decoded) CollectionAssert.AreEqual(first, VoxelStructureCodec.Encode(decoded, names, options), "scenario " + scenario);
                }
                Assert.AreEqual(StableHash.Hash64(VoxelStructureCodec.Encode(structure, names, VoxsEncodeOptions.ForSaveChunk)), structure.ComputeContentHash(names));
            }
        }
        [Test]
        public unsafe void Decode_EqualsOriginal()
        {
            using var fixture = new VoxelTestContent(); var names = VoxsBlockNames.FromCatalog(fixture.Service.Catalog);
            for (int scenario = 0; scenario < 5; scenario++)
            {
                using var original = Scenario(scenario, fixture);
                Assert.IsTrue(VoxelStructureCodec.TryDecode(VoxelStructureCodec.Encode(original, names, VoxsEncodeOptions.ForSaveChunk), fixture.Service.Catalog, out var decoded, out var error), error);
                using (decoded)
                using (var a = VoxelStructureCodec.ToSource(original, Allocator.TempJob))
                using (var b = VoxelStructureCodec.ToSource(decoded, Allocator.TempJob))
                {
                    Assert.AreEqual(a.Source.CellSize, b.Source.CellSize);
                    CollectionAssert.AreEqual(a.Source.Palette.ToArray(), b.Source.Palette.ToArray());
                    CollectionAssert.AreEqual(a.Source.Instances.ToArray(), b.Source.Instances.ToArray());
                    CollectionAssert.AreEqual(a.Source.BrickCoords.ToArray(), b.Source.BrickCoords.ToArray());
                    Assert.AreEqual(a.Source.Cells.Length, b.Source.Cells.Length);
                    if (a.Source.Cells.Length != 0)
                        Assert.AreEqual(0, UnsafeUtility.MemCmp(a.Source.Cells.GetUnsafeReadOnlyPtr(), b.Source.Cells.GetUnsafeReadOnlyPtr(), (long)a.Source.Cells.Length * 4));
                    foreach (var record in original.Data.Bricks)
                        if (record.Page == -1) Assert.AreEqual(-1, decoded.Data.Bricks[decoded.Data.BrickIndex[record.Coord]].Page);
                }
            }
        }
        [Test]
        public void CellIndex_MatchesLocalIndex()
        {
            for (int z = 0; z < 16; z++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                Assert.AreEqual(BrickMath.LocalIndex(x, y, z), VoxsCodec.CellIndex(new int3(x, y, z)));
        }
        [Test]
        public void UnknownId_MapsToMissing()
        {
            using var fixture = new VoxelTestContent();
            using var original = BenchmarkStructures.Ship20k(fixture.Index("block_hull"));
            byte[] bytes = VoxelStructureCodec.Encode(original, VoxsBlockNames.FromCatalog(fixture.Service.Catalog), VoxsEncodeOptions.ForAsset);
            var definitions = new List<ContentDefinition>
            { TestContentFactory.Item("item_missing"), TestContentFactory.Block("block_missing", BlockKind.Structural) };
            var report = new ValidationReport("missing block"); var catalog = ContentCatalog.Build(definitions, report);
            Assert.IsTrue(VoxelStructureCodec.TryDecode(bytes, catalog, out var decoded, out var error), error);
            using (decoded)
            {
                Assert.AreEqual(catalog.GetBlockIndex(catalog.MissingBlock), decoded.Data.Palette[1].Block);
                Assert.AreEqual(20000, decoded.SolidCellCount); Assert.AreEqual(1, catalog.Unresolved.Count);
            }
        }
        [Test, Performance, Category("Performance")]
        public void HollowStation_EncodeDecodeTime()
        {
            using var fixture = new VoxelTestContent(); using var structure = BenchmarkStructures.HollowStation1M(fixture.Index("block_hull"));
            var names = VoxsBlockNames.FromCatalog(fixture.Service.Catalog);
            byte[] bytes = VoxelStructureCodec.Encode(structure, names, VoxsEncodeOptions.ForSaveChunk);
            for (int i = 0; i < 3; i++)
            {
                VoxelStructureCodec.Encode(structure, names, VoxsEncodeOptions.ForSaveChunk);
                Assert.IsTrue(VoxelStructureCodec.TryDecode(bytes, fixture.Service.Catalog, out var warmup, out var error), error); warmup.Dispose();
            }
            var encode = new double[11]; var decode = new double[11];
            for (int i = 0; i < encode.Length; i++)
            {
                var timer = Stopwatch.StartNew(); VoxelStructureCodec.Encode(structure, names, VoxsEncodeOptions.ForSaveChunk); encode[i] = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); bool ok = VoxelStructureCodec.TryDecode(bytes, fixture.Service.Catalog, out var result, out var error); decode[i] = timer.Elapsed.TotalMilliseconds;
                Assert.IsTrue(ok, error); Assert.AreEqual(1008128, result.SolidCellCount); result.Dispose();
            }
            Record("StructureStationEncode", encode, 60); Record("StructureStationDecode", decode, 150);
        }
        static void Record(string name, double[] samples, double budget)
        {
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName + "." + name).AddSamples(name, samples);
            run.Write(name, budget); run.AssertMedian(name, budget);
        }
        [Test]
        public void Tombstones_PreserveIndicesAndAreReusable()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            using var original = new VoxelStructure(); ushort core = fixture.Index("block_core_cross");
            for (int i = 0; i < 3; i++) original.TryPlaceBlock(new BlockPlacement { Block = core, Origin = new int3(i * 10, 0, 0) }, content);
            original.TryRemoveBlockAt(default, content, out _); original.TryRemoveBlockAt(new int3(20, 0, 0), content, out _);
            var names = VoxsBlockNames.FromCatalog(fixture.Service.Catalog); byte[] bytes = VoxelStructureCodec.Encode(original, names, VoxsEncodeOptions.ForSaveChunk);
            Assert.IsTrue(VoxelStructureCodec.TryDecode(bytes, fixture.Service.Catalog, out var result, out var error), error);
            using (result)
            {
                Assert.AreEqual(3, result.Data.Instances.Length); Assert.AreEqual(1, result.GetCell(new int3(10, 0, 0)).InstanceIndex);
                Assert.AreEqual(2, result.Data.FreeInstances.Length);
                CollectionAssert.AreEqual(bytes, VoxelStructureCodec.Encode(result, names, VoxsEncodeOptions.ForSaveChunk));
                Assert.AreEqual(PlaceResult.Ok, result.TryPlaceBlock(new BlockPlacement { Block = core, Origin = new int3(30, 0, 0) }, content));
                Assert.AreEqual(3, result.Data.Instances.Length); Assert.AreEqual(2, result.GetCell(new int3(30, 0, 0)).InstanceIndex);
            }
        }
        [Test]
        public void Encode_OnWorker_EqualsMainThread()
        {
            using var fixture = new VoxelTestContent(); using var structure = BenchmarkStructures.Ship20k(fixture.Index("block_hull"));
            var names = VoxsBlockNames.FromCatalog(fixture.Service.Catalog);
            byte[] main = VoxelStructureCodec.Encode(structure, names, VoxsEncodeOptions.ForAsset);
            byte[] worker = System.Threading.Tasks.Task.Run(() => VoxelStructureCodec.Encode(structure, names, VoxsEncodeOptions.ForAsset)).GetAwaiter().GetResult();
            CollectionAssert.AreEqual(main, worker);
        }
        [Test]
        public void PartialAndTruncatedPayloads_AreRejected()
        {
            using var fixture = new VoxelTestContent(); using var structure = BenchmarkStructures.Ship20k(fixture.Index("block_hull"));
            var names = VoxsBlockNames.FromCatalog(fixture.Service.Catalog);
            using var buffers = VoxelStructureCodec.ToSource(structure, Allocator.TempJob);
            buffers.Source.Partial = true;
            byte[] partial = VoxsCodec.Encode(buffers.Source, names, VoxsEncodeOptions.ForSaveChunk);
            Assert.IsFalse(VoxelStructureCodec.TryDecode(partial, fixture.Service.Catalog, out var result, out var error));
            Assert.IsNull(result); StringAssert.Contains("partial", error);
            buffers.Source.Partial = false;
            byte[] bytes = VoxsCodec.Encode(buffers.Source, names, VoxsEncodeOptions.ForSaveChunk);
            for (int n = 0; n < bytes.Length; n++)
            {
                Assert.IsFalse(VoxelStructureCodec.TryDecode(bytes.AsSpan(0, n), fixture.Service.Catalog, out result, out error));
                Assert.IsNull(result); Assert.IsNotEmpty(error);
            }
            using var empty = new VoxsTestStructures(0, 0);
            Assert.IsTrue(VoxelStructureCodec.TryDecode(VoxsCodec.Encode(empty.Source, names, VoxsEncodeOptions.ForSaveChunk), fixture.Service.Catalog, out result, out error), error);
            using (result)
            {
                Assert.AreEqual(1, result.Data.Palette.Length);
                Assert.AreEqual(PlaceResult.Ok, result.TryPlaceBlock(new BlockPlacement { Block = fixture.Index("block_hull") }, fixture.Content));
            }
        }
    }
}
