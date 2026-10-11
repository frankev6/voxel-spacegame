using System;
using System.Collections.Generic;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.TestTools;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-005: paged structure ownership and the storage contract in SPEC-10 §5.1.</summary>
    public sealed class VoxelStructureStorageTests
    {
        [Test]
        public void SetGet_FuzzMatchesDictionary()
        {
            using var structure = new VoxelStructure();
            var reference = new Dictionary<int3, Cell>();
            var random = new Random(0xABC129u);
            var positions = new int3[128];
            for (int i = 0; i < positions.Length; i++) positions[i] = random.NextInt3(-17, 18);
            uint revision = 0;
            for (int n = 0; n < 100000; n++)
            {
                int3 position = positions[random.NextInt(positions.Length)];
                Cell value = random.NextInt(3) == 0 ? default : Cell.Palette((ushort)random.NextInt(1, 11), (byte)random.NextInt(1, 256));
                reference.TryGetValue(position, out var old);
                if (!old.Equals(value)) revision++;
                structure.SetCell(position, value);
                if (value.IsAir) reference.Remove(position); else reference[position] = value;
                if (!structure.GetCell(position).Equals(value) || structure.Revision != revision || structure.SolidCellCount != reference.Count)
                    Assert.Fail($"Cell/count/revision mismatch at write {n}");
                int3 min = new int3(int.MaxValue), max = new int3(int.MinValue);
                foreach (var pair in reference)
                { min = math.min(min, pair.Key); max = math.max(max, pair.Key); }
                bool hasBounds = structure.TryGetCellBounds(out var actualMin, out var actualMax);
                if (reference.Count == 0) min = max = default;
                if (hasBounds != (reference.Count != 0) || !actualMin.Equals(min) || !actualMax.Equals(max))
                    Assert.Fail($"Bounds mismatch at write {n}");
                if (n % 1000 == 0)
                    foreach (var pair in reference) Assert.AreEqual(pair.Value, structure.GetCell(pair.Key));
            }
            foreach (var pair in reference) Assert.AreEqual(pair.Value, structure.GetCell(pair.Key));
            Assert.AreEqual(default(Cell), structure.GetCell(new int3(900, -200, 400)));
            Assert.AreEqual(default(PaletteEntry), structure.Data.Palette[0]);
        }

        [Test]
        public void EmptyBrick_IsRemovedAndPageReused()
        {
            using var structure = new VoxelStructure();
            var first = new int3(-16, 0, 0); var second = new int3(32, 0, 0);
            structure.SetCell(first, Cell.Palette(1)); structure.SetCell(second, Cell.Palette(2));
            int pageLength = structure.Data.CellPages.Length;
            int page = structure.Data.Bricks[structure.Data.BrickIndex[BrickMath.BrickOf(first)]].Page;
            structure.SetCell(first, default);
            Assert.AreEqual(1, structure.BrickCount);
            Assert.AreEqual(0, structure.Data.BrickIndex[BrickMath.BrickOf(second)]);
            Assert.AreEqual(Cell.Palette(2), structure.GetCell(second));
            var replacement = new int3(70, 4, 2);
            structure.SetCell(replacement, Cell.Palette(3));
            Assert.AreEqual(page, structure.Data.Bricks[structure.Data.BrickIndex[BrickMath.BrickOf(replacement)]].Page);
            Assert.AreEqual(default(Cell), structure.GetCell(new int3(64, 0, 0)));
            Assert.AreEqual(pageLength, structure.Data.CellPages.Length);
            Assert.IsTrue(structure.RemoveBrick(BrickMath.BrickOf(second)));
            Assert.IsFalse(structure.RemoveBrick(BrickMath.BrickOf(second)));
            structure.SetCell(replacement, default);
            Assert.AreEqual(0, structure.BrickCount);
            Assert.IsFalse(structure.TryGetCellBounds(out _, out _));
            Assert.AreEqual(pageLength, structure.Data.CellPages.Length);
            long allocated = structure.NativeBytes;
            Assert.GreaterOrEqual(allocated, pageLength * 4L);
            structure.Clear();
            Assert.AreEqual(1, structure.Data.Palette.Length);
            Assert.AreEqual(0, structure.Data.CellPages.Length);
            structure.SetCell(first, Cell.Palette(4));
            Assert.AreEqual(Cell.Palette(4), structure.GetCell(first));
        }

        [Test]
        public void Uniform_RoundTripAndDensify()
        {
            using var structure = new VoxelStructure(0.3f);
            var cells = new NativeArray<Cell>(4096, Allocator.Temp);
            try
            {
                for (int i = 0; i < cells.Length; i++) cells[i] = Cell.Palette(1);
                int3 coord = new int3(-2, 1, 3);
                structure.SetBrickDense(coord, cells);
                uint revision = structure.Revision;
                structure.OptimizeStorage();
                Assert.AreEqual(revision, structure.Revision);
                Assert.AreEqual(-1, structure.Data.Bricks[0].Page);
                Assert.AreEqual(1, structure.Data.FreePages.Length);
                Assert.IsTrue(structure.Data.Masks[0].IsFull());
                int changedLocal = 731;
                structure.SetCell(BrickMath.CellOf(coord, changedLocal), Cell.Palette(2, 19));
                Assert.AreEqual(0, structure.Data.Bricks[0].Page);
                Assert.AreEqual(0, structure.Data.FreePages.Length);
                Assert.AreEqual(revision + 1, structure.Data.Bricks[0].ChangedRevision);
                for (int i = 0; i < 4096; i++)
                    Assert.AreEqual(i == changedLocal ? Cell.Palette(2, 19) : Cell.Palette(1), structure.GetCell(BrickMath.CellOf(coord, i)));
                structure.OptimizeStorage(); Assert.GreaterOrEqual(structure.Data.Bricks[0].Page, 0);
                structure.SetBrickUniform(coord, Cell.Palette(3));
                Assert.AreEqual(-1, structure.Data.Bricks[0].Page);
                Assert.AreEqual(4096, structure.SolidCellCount);
                structure.SetBrickDense(coord, cells);
                Assert.AreEqual(4096, structure.Data.CellPages.Length);
                for (int i = 0; i < 4096; i++) cells[i] = default;
                cells[4095] = Cell.Palette(4);
                structure.SetBrickDense(coord, cells);
                Assert.AreEqual(1, structure.SolidCellCount);
                Assert.AreEqual(1, structure.Data.Masks[0].CountSolid());
                cells[4095] = default; structure.SetBrickDense(coord, cells);
                Assert.AreEqual(0, structure.BrickCount);
            }
            finally { cells.Dispose(); }
        }

        [Test]
        public void SortedOrder_IsZYX()
        {
            using var structure = new VoxelStructure();
            var coords = new[] { new int3(4, 3, -2), new int3(1, 2, 4), new int3(-1, 2, 4), new int3(0, 1, 4), new int3(-4, 3, -2) };
            foreach (int3 coord in coords) structure.SetBrickUniform(coord, Cell.Palette(1));
            using var order = structure.SortedBrickOrder(Allocator.Temp);
            var expected = new[] { 4, 0, 3, 2, 1 };
            for (int i = 0; i < order.Length; i++) Assert.AreEqual(coords[expected[i]], structure.Data.Bricks[order[i]].Coord);
        }

        [Test]
        public void Pinned_MutatorsThrow()
        {
            using var structure = new VoxelStructure();
            using var cells = new NativeArray<Cell>(4096, Allocator.Temp);
            structure.Pin(); structure.Pin();
            try
            {
                Assert.IsTrue(structure.IsPinned);
                Assert.Throws<InvalidOperationException>(() => structure.SetCell(default, Cell.Palette(1)));
                Assert.Throws<InvalidOperationException>(() => structure.SetBrickDense(default, cells));
                Assert.Throws<InvalidOperationException>(() => structure.SetBrickUniform(default, Cell.Palette(1)));
                Assert.Throws<InvalidOperationException>(() => structure.RemoveBrick(default));
                Assert.Throws<InvalidOperationException>(() => structure.Clear());
                Assert.Throws<InvalidOperationException>(() => structure.OptimizeStorage());
                Assert.Throws<InvalidOperationException>(() => structure.SetReadOnly());
                Assert.Throws<InvalidOperationException>(() => structure.Dispose());
            }
            finally { structure.Unpin(); structure.Unpin(); }
            Assert.IsFalse(structure.IsPinned);
            Assert.Throws<InvalidOperationException>(() => structure.Unpin());
        }

        [Test]
        public void ReadOnly_MutatorsReject()
        {
            using var structure = new VoxelStructure();
            using var cells = new NativeArray<Cell>(4096, Allocator.Temp);
            structure.SetReadOnly(); Assert.IsTrue(structure.IsReadOnly);
            Assert.Throws<InvalidOperationException>(() => structure.SetCell(default, Cell.Palette(1)));
            Assert.Throws<InvalidOperationException>(() => structure.SetBrickDense(default, cells));
            Assert.Throws<InvalidOperationException>(() => structure.SetBrickUniform(default, Cell.Palette(1)));
            Assert.Throws<InvalidOperationException>(() => structure.RemoveBrick(default));
            Assert.Throws<InvalidOperationException>(() => structure.Clear());
            Assert.Throws<InvalidOperationException>(() => structure.OptimizeStorage());
            structure.Pin(); structure.Unpin();
            Assert.IsFalse(structure.TryGetCellBounds(out _, out _));
        }

        [Test]
        public void Dispose_NoLeaks()
        {
            var previous = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try
            {
                using (var warmup = new VoxelStructure()) warmup.SetCell(default, Cell.Palette(1));
                UnsafeUtility.CheckForLeaks();
                var structure = new VoxelStructure();
                structure.SetCell(default, Cell.Palette(1));
                structure.SetBrickUniform(new int3(1), Cell.Palette(2));
                structure.Dispose(); structure.Dispose();
                Assert.IsFalse(structure.Data.IsCreated); Assert.AreEqual(0, structure.NativeBytes);
                UnsafeUtility.CheckForLeaks(); LogAssert.NoUnexpectedReceived();
            }
            finally { NativeLeakDetection.Mode = previous; }
        }
    }
}
