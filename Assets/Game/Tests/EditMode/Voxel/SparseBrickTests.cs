using Game.Voxel.Data;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-003: sparse/dense semantic parity and the exact 1024-entry transition.</summary>
    public sealed class SparseBrickTests
    {
        static Entity Create(EntityManager em, bool dense)
        {
            Entity entity = em.CreateEntity(typeof(BrickCells), typeof(BrickSparseCells), typeof(BrickSolidMask));
            if (dense) em.GetBuffer<BrickCells>(entity).Resize(4096, NativeArrayOptions.ClearMemory);
            em.GetBuffer<BrickSolidMask>(entity).Resize(64, NativeArrayOptions.ClearMemory);
            return entity;
        }
        static Cell Read(EntityManager em, Entity entity, int local)
            => BrickWrite.Get(em.GetBuffer<BrickCells>(entity), em.GetBuffer<BrickSparseCells>(entity), local);
        static bool Write(EntityManager em, Entity entity, ref Brick brick, ref BrickDirty dirty, int local, Cell cell, uint revision)
            => BrickWrite.Set(ref brick, em.GetBuffer<BrickCells>(entity), em.GetBuffer<BrickSparseCells>(entity), em.GetBuffer<BrickSolidMask>(entity), ref dirty, local, cell, revision);

        [Test] public void SparseDense_Equivalent_Fuzz()
        {
            Assert.AreEqual(6, UnsafeUtility.SizeOf<BrickSparseCells>());
            using var fixture = new VoxelTestContent();
            using var source = new VoxelStructure();
            source.FindOrAddPalette(new PaletteEntry { Block = fixture.Index("block_hull") });
            source.FindOrAddPalette(new PaletteEntry { Block = fixture.Index("block_hull_angled") });
            using var world = new World("SparseBrickFuzz"); var em = world.EntityManager;
            Entity sparseEntity = Create(em, false), denseEntity = Create(em, true);
            var sparseBrick = new Brick(); var denseBrick = new Brick(); var reference = new Cell[4096]; var rng = new Random(92137);
            for (int step = 0; step < 30000; step++)
            {
                // First phase remains sparse; second crosses the threshold into dense storage.
                int local = rng.NextInt(step < 10000 ? 768 : 4096);
                Cell value = rng.NextInt(4) == 0 ? default : Cell.Palette((ushort)rng.NextInt(1, 3), (byte)rng.NextInt(1, 256), (byte)rng.NextInt(4));
                var a = new BrickDirty(); var b = new BrickDirty();
                bool changed = !reference[local].Equals(value);
                Assert.AreEqual(changed, Write(em, sparseEntity, ref sparseBrick, ref a, local, value, (uint)step + 1));
                Assert.AreEqual(changed, Write(em, denseEntity, ref denseBrick, ref b, local, value, (uint)step + 1));
                source.SetCell(BrickMath.LocalFromIndex(local), value); reference[local] = value;
                Assert.AreEqual(a, b); Assert.AreEqual(sparseBrick, denseBrick);
                Assert.AreEqual(value, Read(em, sparseEntity, local)); Assert.AreEqual(value, source.GetCell(BrickMath.LocalFromIndex(local)));
                if (step % 1000 != 0) continue;
                for (int i = 0; i < 4096; i++)
                {
                    Assert.AreEqual(reference[i], Read(em, sparseEntity, i)); Assert.AreEqual(reference[i], Read(em, denseEntity, i));
                    Assert.AreEqual(reference[i], source.GetCell(BrickMath.LocalFromIndex(i)));
                }
                CollectionAssert.AreEqual(em.GetBuffer<BrickSolidMask>(denseEntity).AsNativeArray().ToArray(), em.GetBuffer<BrickSolidMask>(sparseEntity).AsNativeArray().ToArray());
            }
            using var clone = source.Clone(); using var diff = VoxelStructure.Diff(source, clone, Allocator.TempJob);
            Assert.AreEqual(0, diff.Added.Length); Assert.AreEqual(0, diff.Removed.Length);
            // Reduce below the threshold, explicitly repack, and compare the unchanged VOXS byte format and mass.
            for (int i = 768; i < 4096; i++) source.SetCell(BrickMath.LocalFromIndex(i), default);
            using var denseSource = source.Clone(); source.OptimizeStorage();
            Assert.LessOrEqual(source.Data.Bricks[0].Page, -2); Assert.GreaterOrEqual(denseSource.Data.Bricks[0].Page, 0);
            Assert.AreEqual(denseSource.GetMass(fixture.Content), source.GetMass(fixture.Content));
            var names = VoxsBlockNames.FromCatalog(fixture.Service.Catalog);
            CollectionAssert.AreEqual(VoxelStructureCodec.Encode(denseSource, names, VoxsEncodeOptions.ForSaveChunk), VoxelStructureCodec.Encode(source, names, VoxsEncodeOptions.ForSaveChunk));
        }

        [Test] public void DensifyAtThreshold()
        {
            using var source = new VoxelStructure(); using var world = new World("SparseBrickThreshold"); var em = world.EntityManager;
            Entity entity = Create(em, false); var brick = new Brick(); var dirty = new BrickDirty();
            for (int i = 1023; i >= 0; i--)
            {
                Cell value = Cell.Palette(1, (byte)(i % 255 + 1));
                source.SetCell(BrickMath.LocalFromIndex(i), value); Write(em, entity, ref brick, ref dirty, i, value, 1);
            }
            Assert.AreEqual(1024, em.GetBuffer<BrickSparseCells>(entity).Length); Assert.AreEqual(0, em.GetBuffer<BrickCells>(entity).Length);
            Assert.LessOrEqual(source.Data.Bricks[0].Page, -2);
            var sparse = em.GetBuffer<BrickSparseCells>(entity);
            for (int i = 0; i < sparse.Length; i++) Assert.AreEqual(i, sparse[i].Local);
            source.SetCell(BrickMath.LocalFromIndex(2048), Cell.Palette(2)); Write(em, entity, ref brick, ref dirty, 2048, Cell.Palette(2), 2);
            Assert.GreaterOrEqual(source.Data.Bricks[0].Page, 0); Assert.AreEqual(4096, em.GetBuffer<BrickCells>(entity).Length);
            Assert.AreEqual(0, sparse.Length); Assert.AreEqual(0, sparse.Capacity, "release sparse heap after densification");
            for (int i = 0; i < 4096; i++) Assert.AreEqual(source.GetCell(BrickMath.LocalFromIndex(i)), Read(em, entity, i));
            Assert.AreEqual(1025, brick.SolidCount); Assert.AreEqual(1025, source.SolidCellCount);
        }
    }
}
