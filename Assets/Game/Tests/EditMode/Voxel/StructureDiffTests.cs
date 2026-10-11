using System.Collections.Generic;
using System.Diagnostics;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-007: deep ownership, resolved-placement differences and the 100k-cell diff gate.</summary>
    public sealed class StructureDiffTests
    {
        static void Edit(VoxelStructure structure, VoxelTestContent fixture, uint seed)
        {
            var content = fixture.Content; var rng = new Random(seed);
            var types = new[] { fixture.Index("block_hull"), fixture.Index("block_hull_angled"), fixture.Index("block_thruster_medium"), fixture.Index("block_core_cross"), fixture.Index("block_capacitor_small") };
            for (int i = 0; i < 1000; i++)
            {
                int3 origin = rng.NextInt3(-4, 5) * 4;
                if (rng.NextBool()) structure.TryRemoveBlockAt(origin, content, out _);
                else structure.TryPlaceBlock(new BlockPlacement { Block = types[rng.NextInt(types.Length)], Origin = origin, Orient = (byte)rng.NextInt(16), Color = (byte)rng.NextInt(8) }, content);
            }
        }
        static HashSet<BlockPlacement> Placements(VoxelStructure structure)
        {
            using var list = new NativeList<StructureBlock>(Allocator.Temp);
            structure.GetBlocks(list); var set = new HashSet<BlockPlacement>();
            foreach (var block in list) Assert.IsTrue(set.Add(block.Placement));
            return set;
        }
        [Test]
        public void CloneDiff_IsEmpty()
        {
            using var fixture = new VoxelTestContent(); using var source = new VoxelStructure();
            Edit(source, fixture, 19); source.SetReadOnly(); source.Pin();
            try
            {
                using var clone = source.Clone();
                Assert.IsFalse(clone.IsReadOnly); Assert.IsFalse(clone.IsPinned);
                using var diff = VoxelStructure.Diff(source, clone, Allocator.TempJob);
                Assert.AreEqual(0, diff.Added.Length); Assert.AreEqual(0, diff.Removed.Length);
                Assert.AreEqual(clone.Data.CellPages.Length, clone.Data.CellPages.Capacity);
                Assert.AreEqual(clone.Data.SparsePages.Length, clone.Data.SparsePages.Capacity);
                Assert.IsTrue(Placements(source).SetEquals(Placements(clone)));
                int count = source.SolidCellCount; clone.Clear();
                Assert.AreEqual(count, source.SolidCellCount); Assert.Greater(count, 0);
            }
            finally { source.Unpin(); }
        }
        [Test]
        public void Diff_ApplyReproducesTarget()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            using var from = new VoxelStructure(); Edit(from, fixture, 31);
            using var target = from.Clone(); Edit(target, fixture, 32); target.Compact();
            using var diff = VoxelStructure.Diff(from, target, Allocator.TempJob);
            using var applied = from.Clone();
            foreach (var removed in diff.Removed)
            {
                Assert.IsTrue(applied.TryRemoveBlockAt(removed.Origin, content, out var actual));
                Assert.AreEqual(removed, actual);
            }
            foreach (var added in diff.Added) Assert.AreEqual(PlaceResult.Ok, applied.TryPlaceBlock(added, content));
            Assert.IsTrue(Placements(target).SetEquals(Placements(applied)));
            using var remainder = VoxelStructure.Diff(applied, target, Allocator.TempJob);
            Assert.AreEqual(0, remainder.Removed.Length); Assert.AreEqual(0, remainder.Added.Length);
        }
        [Test]
        public void Diff_IgnoresHp()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            using var from = new VoxelStructure();
            from.TryPlaceBlock(new BlockPlacement { Block = fixture.Index("block_hull") }, content);
            from.TryPlaceBlock(new BlockPlacement { Block = fixture.Index("block_core_cross"), Origin = new int3(16, 0, 0) }, content);
            using var to = from.Clone();
            var cell = to.GetCell(default); cell.Hp = 3; cell.Flags = (byte)CellFlags.Scorched; to.SetCell(default, cell);
            cell = to.GetCell(new int3(16, 0, 0)); cell.Hp = 64; to.SetCell(new int3(16, 0, 0), cell);
            var data = to.Data; var instance = data.Instances[0]; instance.FunctionHp = 2; instance.Flags = BlockInstanceFlags.Destroyed; data.Instances[0] = instance;
            using var diff = VoxelStructure.Diff(from, to, Allocator.TempJob);
            Assert.AreEqual(0, diff.Removed.Length); Assert.AreEqual(0, diff.Added.Length);
        }
        [Test, Performance, Category("Performance")]
        public void Diff_Perf100k()
        {
            using var fixture = new VoxelTestContent(); using var from = new VoxelStructure();
            ushort block = fixture.Index("block_hull");
            ushort original = from.FindOrAddPalette(new PaletteEntry { Block = block });
            ushort painted = from.FindOrAddPalette(new PaletteEntry { Block = block, Color = 1 });
            var cells = new NativeArray<Cell>(4096, Allocator.Temp);
            try
            {
                for (int b = 0, remaining = 100000; remaining > 0; b++, remaining -= 4096)
                {
                    for (int i = 0; i < cells.Length; i++) cells[i] = i < remaining ? Cell.Palette(original) : default;
                    from.SetBrickDense(new int3(b, 0, 0), cells);
                }
            }
            finally { cells.Dispose(); }
            Assert.AreEqual(100000, from.SolidCellCount);
            using var to = from.Clone();
            for (int i = 0; i < 10; i++) to.SetCell(new int3(i * 16, 0, 0), Cell.Palette(painted));
            for (int i = 0; i < 5; i++) { using var warmup = VoxelStructure.Diff(from, to, Allocator.TempJob); }
            var samples = new double[21];
            for (int i = 0; i < samples.Length; i++)
            {
                var watch = Stopwatch.StartNew();
                using var diff = VoxelStructure.Diff(from, to, Allocator.TempJob);
                samples[i] = watch.Elapsed.TotalMilliseconds;
                Assert.AreEqual(10, diff.Removed.Length); Assert.AreEqual(10, diff.Added.Length);
            }
            PerfRun.Gate("StructureDiff100k", samples, 10);
        }
    }
}
