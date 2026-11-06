using Game.Voxel.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Tests.EditMode.Voxel
{
    public sealed unsafe class VoxsCellLoopTests
    {
        [TestCase(true), TestCase(false)]
        public void CellLoops_MatchManagedAtValidationBoundaries(bool burst)
        {
            bool previous = BurstCompiler.Options.EnableBurstCompilation;
            bool synchronous = BurstCompiler.Options.EnableBurstCompileSynchronously;
            try
            {
                BurstCompiler.Options.EnableBurstCompilation = burst;
                BurstCompiler.Options.EnableBurstCompileSynchronously = true;
                using var storage = new NativeArray<Cell>(4096, Allocator.Temp);
                var cells = storage;
                var ptr = (Cell*)cells.GetUnsafePtr();
                foreach (int invalidAt in new[] { -1, 0, 2048, 4095 })
                {
                    for (int i = 0; i < 4096; i++)
                        cells[i] = new Cell { Ref = (ushort)(i % 3 == 0 ? 0 : i % 3 == 1 ? 3 : 0x8000 | (i % 32)), Hp = 17, Flags = 5 };
                    if (invalidAt >= 0) cells[invalidAt] = new Cell { Ref = 0x8020, Hp = 17, Flags = 5 };
                    var expected = cells.ToArray(); int solid = 0;
                    for (int i = 0; i < expected.Length; i++)
                    {
                        var c = expected[i]; if (c.Ref == 0) continue;
                        if (c.IsInstance ? c.InstanceIndex >= 32 : c.Ref >= 4) { solid = -1; break; }
                        expected[i].Hp = 255; solid++;
                    }
                    Assert.AreEqual(solid, VoxsCellLoops.ValidateReferencesAndInitializeHp(ptr, 32, 4));
                    CollectionAssert.AreEqual(expected, cells.ToArray());
                    Assert.AreEqual(1, VoxsCellLoops.ValidateHp(ptr));
                    cells[4095] = new Cell { Ref = 1, Hp = 0 };
                    Assert.AreEqual(0, VoxsCellLoops.ValidateHp(ptr));
                }
                VoxsCellLoops.FillReferences(ptr, 0);
                Assert.AreEqual(0, VoxsCellLoops.ValidateReferencesAndInitializeHp(ptr, 0, 0));
                Assert.AreEqual(1, VoxsCellLoops.ValidateHp(ptr));
                VoxsCellLoops.FillReferences(ptr, 4);
                Assert.AreEqual(-1, VoxsCellLoops.ValidateReferencesAndInitializeHp(ptr, 32, 4));
                VoxsCellLoops.FillReferences(ptr, 0xffff);
                Assert.AreEqual(4096, VoxsCellLoops.ValidateReferencesAndInitializeHp(ptr, 32768, 4));
                for (int i = 0; i < 4096; i++) cells[i] = new Cell { Ref = (ushort)(i % 3 == 0 ? 1 : 0x8000 | (i % 32)), Hp = 37, Flags = 7 };
                var counts = new int[32]; var expectedCounts = new int[32]; var remap = new ushort[32];
                for (int i = 0; i < 32; i++) remap[i] = (ushort)(i % 2 == 0 ? 2 : 0x8000 | (31 - i));
                var mapped = cells.ToArray();
                for (int i = 0; i < mapped.Length; i++) if (mapped[i].IsInstance)
                { expectedCounts[mapped[i].InstanceIndex]++; mapped[i].Ref = remap[mapped[i].InstanceIndex]; }
                fixed (int* countPointer = counts) VoxsCellLoops.CountInstances(ptr, 4096, countPointer);
                fixed (ushort* remapPointer = remap) VoxsCellLoops.RemapInstances(ptr, 4096, remapPointer);
                CollectionAssert.AreEqual(expectedCounts, counts); CollectionAssert.AreEqual(mapped, cells.ToArray());
            }
            finally
            {
                BurstCompiler.Options.EnableBurstCompilation = previous;
                BurstCompiler.Options.EnableBurstCompileSynchronously = synchronous;
            }
        }
    }
}
