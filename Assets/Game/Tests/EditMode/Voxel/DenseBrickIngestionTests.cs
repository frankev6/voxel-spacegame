using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    public sealed class DenseBrickIngestionTests
    {
        [TestCase(true), TestCase(false)]
        public void DenseIngestion_MatchesLegacyAcrossStorageTransitions(bool burst)
        {
            bool previous = BurstCompiler.Options.EnableBurstCompilation;
            bool synchronous = BurstCompiler.Options.EnableBurstCompileSynchronously;
            try
            {
                BurstCompiler.Options.EnableBurstCompilation = burst;
                BurstCompiler.Options.EnableBurstCompileSynchronously = true;
                using var expected = new VoxelStructure(); using var actual = new VoxelStructure();
                using var cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Temp);
                int3 coord = new int3(-1, 2, 3);
                foreach (int count in new[] { 0, 1, SparseCellStorage.Limit, 1, SparseCellStorage.Limit + 1, SparseCellStorage.Limit, 4096, 1, 0, 4096 })
                {
                    // Noncanonical air payload must be cleared; non-air HP/flags must remain exact.
                    for (int i = 0; i < cells.Length; i++) cells[i] = i < count
                        ? Cell.Palette(1, 37, 5) : new Cell { Hp = 99, Flags = 255 };
                    expected.SetBrickDenseLegacy(coord, cells); actual.SetBrickDense(coord, cells); Equal(expected, actual, coord);
                    uint revision = actual.Revision;
                    expected.SetBrickDenseLegacy(coord, cells); actual.SetBrickDense(coord, cells); Equal(expected, actual, coord);
                    Assert.AreEqual(revision, actual.Revision, "Identical replacement must not change revision");
                    expected.OptimizeStorage(); actual.OptimizeStorage(); Equal(expected, actual, coord);
                }
                // Replace an optimized uniform brick and then reuse retired dense/sparse pages at another coordinate.
                cells[17] = Cell.Palette(2, 19, 7);
                expected.SetBrickDenseLegacy(coord, cells); actual.SetBrickDense(coord, cells); Equal(expected, actual, coord);
                coord = new int3(8, -2, 0);
                expected.SetBrickDenseLegacy(coord, cells); actual.SetBrickDense(coord, cells); Equal(expected, actual, coord);
            }
            finally
            {
                BurstCompiler.Options.EnableBurstCompilation = previous;
                BurstCompiler.Options.EnableBurstCompileSynchronously = synchronous;
            }
        }
        static void Equal(VoxelStructure expected, VoxelStructure actual, int3 coord)
        {
            Assert.AreEqual(expected.Revision, actual.Revision); Assert.AreEqual(expected.SolidCellCount, actual.SolidCellCount);
            var a = expected.Data; var b = actual.Data;
            Assert.AreEqual(a.Bricks.Length, b.Bricks.Length);
            for (int i = 0; i < a.Bricks.Length; i++)
            {
                Assert.AreEqual(a.Bricks[i], b.Bricks[i]);
                for (int bit = 0; bit < BrickMath.CellsPerBrick; bit++)
                    Assert.AreEqual(a.Masks[i].Get(bit), b.Masks[i].Get(bit));
            }
            CollectionAssert.AreEqual(a.FreePages.AsArray().ToArray(), b.FreePages.AsArray().ToArray());
            CollectionAssert.AreEqual(a.FreeSparsePages.AsArray().ToArray(), b.FreeSparsePages.AsArray().ToArray());
            CollectionAssert.AreEqual(a.CellPages.AsArray().ToArray(), b.CellPages.AsArray().ToArray());
            CollectionAssert.AreEqual(a.SparsePages.AsArray().ToArray(), b.SparsePages.AsArray().ToArray());
            for (int i = 0; i < BrickMath.CellsPerBrick; i++)
            {
                var cell = BrickMath.CellOf(coord, i);
                Assert.AreEqual(expected.GetCell(cell), actual.GetCell(cell));
            }
        }
    }
}
