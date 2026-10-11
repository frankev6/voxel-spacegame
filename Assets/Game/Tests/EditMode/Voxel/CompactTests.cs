using System.Collections.Generic;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-006: canonical compaction preserves resolved placements after seed-7 edit histories.</summary>
    public sealed class CompactTests
    {
        static HashSet<BlockPlacement> Edit(VoxelStructure structure, VoxelTestContent fixture)
        {
            var content = fixture.Content;
            var blocks = new[] { fixture.Index("block_hull"), fixture.Index("block_hull_angled"), fixture.Index("block_thruster_medium"), fixture.Index("block_core_cross"), fixture.Index("block_capacitor_small") };
            var expected = new HashSet<BlockPlacement>(); var rng = new Random(7);
            for (int i = 0; i < 5000; i++)
            {
                int3 origin = new int3(rng.NextInt(-4, 4), rng.NextInt(-1, 1), rng.NextInt(-2, 2)) * 4;
                if (rng.NextInt(3) == 0)
                {
                    if (structure.TryRemoveBlockAt(origin, content, out var removed)) expected.Remove(removed);
                }
                else
                {
                    var p = new BlockPlacement { Block = blocks[rng.NextInt(blocks.Length)], Origin = origin, Orient = (byte)rng.NextInt(16), Color = (byte)rng.NextInt(8) };
                    if (structure.TryPlaceBlock(p, content) == PlaceResult.Ok) expected.Add(p);
                }
            }
            // Force an unused palette entry and an instance tombstone independently of the random end state.
            structure.FindOrAddPalette(new PaletteEntry { Block = blocks[0], Color = 200 });
            var garbage = new BlockPlacement { Block = blocks[3], Origin = new int3(1000) };
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(garbage, content));
            Assert.IsTrue(structure.TryRemoveBlockAt(garbage.Origin, content, out _));
            // Include a uniform brick so compaction remaps that representation as well as dense pages.
            ushort uniform = structure.FindOrAddPalette(new PaletteEntry { Block = blocks[1], Color = 201 });
            int3 brickCoord = new int3(3);
            structure.SetBrickUniform(brickCoord, Cell.Palette(uniform, 91, (byte)CellFlags.Scorched));
            for (int i = 0; i < 4096; i++) expected.Add(new BlockPlacement { Block = blocks[1], Color = 201, Origin = BrickMath.CellOf(brickCoord, i) });
            return expected;
        }
        static HashSet<BlockPlacement> Blocks(VoxelStructure structure)
        {
            using var list = new NativeList<StructureBlock>(Allocator.Temp);
            structure.GetBlocks(list);
            var result = new HashSet<BlockPlacement>();
            foreach (var item in list) Assert.IsTrue(result.Add(item.Placement), "duplicate placement");
            return result;
        }
        [Test]
        public void Compact_PreservesBlocks_RemovesGarbage()
        {
            using var fixture = new VoxelTestContent(); using var structure = new VoxelStructure();
            var expected = Edit(structure, fixture);
            Assert.IsTrue(expected.SetEquals(Blocks(structure)));
            uint before = structure.Revision; long bytes = structure.NativeBytes;
            structure.Compact();
            Assert.AreEqual(before + 1, structure.Revision);
            Assert.IsTrue(expected.SetEquals(Blocks(structure)));
            Assert.Less(structure.NativeBytes, bytes);
            var data = structure.Data; var used = new HashSet<ushort>();
            for (int b = 0; b < data.Bricks.Length; b++)
            {
                var record = data.Bricks[b]; Assert.AreEqual(b, data.BrickIndex[record.Coord]); Assert.AreEqual(structure.Revision, record.ChangedRevision);
                for (int i = 0; i < 4096; i++)
                {
                    Cell cell = structure.GetCell(BrickMath.CellOf(record.Coord, i));
                    if (!cell.IsAir && !cell.IsInstance) used.Add(cell.PaletteIndex);
                    if (cell.IsInstance) Assert.AreNotEqual(0, data.Instances[cell.InstanceIndex].Block);
                }
            }
            Assert.AreEqual(data.Palette.Length - 1, used.Count);
            foreach (var instance in data.Instances) Assert.AreNotEqual(0, instance.Block);
            Assert.AreEqual(0, data.FreeInstances.Length); Assert.AreEqual(0, data.FreePages.Length); Assert.AreEqual(0, data.FreeSparsePages.Length);
            Assert.AreEqual(data.CellPages.Length, data.CellPages.Capacity); Assert.AreEqual(data.SparsePages.Length, data.SparsePages.Capacity);
            Assert.AreEqual(data.Bricks.Length, data.Bricks.Capacity); Assert.AreEqual(data.Masks.Length, data.Masks.Capacity);
            Assert.AreEqual(data.Palette.Length, data.Palette.Capacity); Assert.AreEqual(data.Instances.Length, data.Instances.Capacity);
            Assert.AreEqual(91, structure.GetCell(new int3(48)).Hp); Assert.AreEqual((byte)CellFlags.Scorched, structure.GetCell(new int3(48)).Flags);
        }
        [Test]
        public void Compact_IsDeterministic()
        {
            using var fixture = new VoxelTestContent(); using var first = new VoxelStructure(); using var second = new VoxelStructure();
            Edit(first, fixture); Edit(second, fixture);
            first.Compact(); second.Compact();
            // Compare semantic field values; never treat struct padding bytes as canonical state.
            CollectionAssert.AreEqual(first.Data.Palette.AsArray().ToArray(), second.Data.Palette.AsArray().ToArray());
            CollectionAssert.AreEqual(first.Data.Instances.AsArray().ToArray(), second.Data.Instances.AsArray().ToArray());
            CollectionAssert.AreEqual(first.Data.Bricks.AsArray().ToArray(), second.Data.Bricks.AsArray().ToArray());
            CollectionAssert.AreEqual(first.Data.CellPages.AsArray().ToArray(), second.Data.CellPages.AsArray().ToArray());
            CollectionAssert.AreEqual(first.Data.SparsePages.AsArray().ToArray(), second.Data.SparsePages.AsArray().ToArray());
            using var order = first.SortedBrickOrder(Allocator.Temp);
            for (int i = 0; i < order.Length; i++) Assert.AreEqual(i, order[i]);
            var palette = first.Data.Palette.AsArray().ToArray(); var cells = first.Data.CellPages.AsArray().ToArray(); var sparse = first.Data.SparsePages.AsArray().ToArray();
            first.Compact();
            CollectionAssert.AreEqual(palette, first.Data.Palette.AsArray().ToArray());
            CollectionAssert.AreEqual(cells, first.Data.CellPages.AsArray().ToArray());
            CollectionAssert.AreEqual(sparse, first.Data.SparsePages.AsArray().ToArray());
        }
    }
}
