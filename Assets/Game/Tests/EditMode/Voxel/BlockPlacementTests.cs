using System.IO;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-006: instance footprints, atomic placement rejection and slot reuse (regression 01-B1).</summary>
    public sealed class BlockPlacementTests
    {
        [Test]
        public void MultiCell_AllOrientations_FootprintMatches()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            ushort block = fixture.Index("block_thruster_medium");
            ref var def = ref content.Blocks.Value.Blocks[block];
            for (byte orient = 0; orient < 24; orient++)
            {
                using var structure = new VoxelStructure();
                var placement = new BlockPlacement { Block = block, Origin = new int3(3, 4, 5), Orient = orient, Color = 7 };
                bool allowed = (def.AllowedOrientations & (1u << orient)) != 0;
                Assert.AreEqual(allowed ? PlaceResult.Ok : PlaceResult.OrientationNotAllowed, structure.TryPlaceBlock(placement, content));
                if (!allowed) { Assert.AreEqual(0, structure.SolidCellCount); continue; }
                Assert.AreEqual(10, structure.SolidCellCount); Assert.AreEqual(1, structure.Data.Instances.Length);
                var instance = structure.Data.Instances[0];
                Assert.AreEqual(10, instance.CellsAlive); Assert.AreEqual(1000, instance.FunctionHp);
                Assert.AreEqual(placement.Origin, instance.Origin); Assert.AreEqual(orient, instance.Orient); Assert.AreEqual(7, instance.Color);
                for (int i = 0; i < def.Cells.Length; i++)
                {
                    int3 cell = placement.Origin + math.mul(content.Orientations.Value.Matrices[orient], def.Cells[i]);
                    Assert.AreEqual(Cell.Instance(0, 255, i == 0 ? (byte)CellFlags.InstanceRoot : (byte)0), structure.GetCell(cell));
                    Assert.IsTrue(structure.TryGetBlockAt(cell, out var read, out int index));
                    Assert.AreEqual(placement, read); Assert.AreEqual(0, index);
                }
            }
        }
        [Test]
        public void RemoveFromAnyCell_ClearsFootprint()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            ushort block = fixture.Index("block_thruster_medium"); ref var def = ref content.Blocks.Value.Blocks[block];
            for (byte orient = 0; orient < 16; orient++) for (int k = 0; k < 10; k++)
            {
                using var structure = new VoxelStructure();
                var placement = new BlockPlacement { Block = block, Origin = new int3(15, 15, -16), Orient = orient };
                Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(placement, content));
                int3 cell = placement.Origin + math.mul(content.Orientations.Value.Matrices[orient], def.Cells[k]);
                Assert.IsTrue(structure.TryRemoveBlockAt(cell, content, out var removed));
                Assert.AreEqual(placement, removed); Assert.AreEqual(0, structure.SolidCellCount); Assert.AreEqual(0, structure.BrickCount);
                Assert.AreEqual(default(BlockInstance), structure.Data.Instances[0]); Assert.AreEqual(0, structure.Data.FreeInstances[0]);
                Assert.IsFalse(structure.TryRemoveBlockAt(cell, content, out _));
            }
        }
        [Test]
        public void Occupied_NoChange()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            using var structure = new VoxelStructure();
            var origin = new int3(3, 4, 5);
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(new BlockPlacement { Block = fixture.Index("block_hull"), Origin = origin + new int3(2, 0, 1) }, content));
            byte[] before = Snapshot(structure);
            Assert.AreEqual(PlaceResult.Occupied, structure.TryPlaceBlock(new BlockPlacement { Block = fixture.Index("block_thruster_medium"), Origin = origin }, content));
            CollectionAssert.AreEqual(before, Snapshot(structure));
        }
        [Test]
        public void PaletteDedup()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            using var structure = new VoxelStructure();
            var first = new BlockPlacement { Block = fixture.Index("block_hull"), Color = 3, Orient = 4 };
            var second = first; second.Origin = new int3(-17, 0, 0);
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(first, content));
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(second, content));
            Assert.AreEqual(2, structure.Data.Palette.Length); Assert.AreEqual(0, structure.Data.Instances.Length);
            Assert.AreEqual(structure.GetCell(first.Origin).Ref, structure.GetCell(second.Origin).Ref);
            Assert.IsTrue(structure.TryGetBlockAt(second.Origin, out var read, out int index));
            Assert.AreEqual(second, read); Assert.AreEqual(-1, index);
            Assert.IsTrue(structure.TryRemoveBlockAt(first.Origin, content, out var removed)); Assert.AreEqual(first, removed);
            Assert.AreEqual(1, structure.SolidCellCount);
        }
        [Test]
        public void InstanceSlotReuse()
        {
            using var fixture = new VoxelTestContent(); var content = fixture.Content;
            using var structure = new VoxelStructure();
            var first = new BlockPlacement { Block = fixture.Index("block_core_cross") };
            var second = new BlockPlacement { Block = fixture.Index("block_capacitor_small"), Origin = new int3(-32, 1, 4) };
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(first, content));
            Assert.IsTrue(structure.TryRemoveBlockAt(first.Origin, content, out _));
            Assert.AreEqual(PlaceResult.Ok, structure.TryPlaceBlock(second, content));
            Assert.AreEqual(1, structure.Data.Instances.Length); Assert.AreEqual(0, structure.Data.FreeInstances.Length);
            Assert.AreEqual(0, structure.GetCell(second.Origin).InstanceIndex); Assert.AreEqual(second.Block, structure.Data.Instances[0].Block);
            Assert.AreEqual(400, structure.Data.Instances[0].FunctionHp); Assert.AreEqual(4, structure.Data.Instances[0].CellsAlive);
        }

        // Compare the unchanged storage, not a persisted format or hash; native element bytes include no pointers.
        internal static byte[] Snapshot(VoxelStructure structure)
        {
            var data = structure.Data; var h = data.Header.Value;
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(h.Revision); writer.Write(h.CellSize); writer.Write(h.BoundsValid); writer.Write(h.ReadOnly); writer.Write(h.PinCount);
            writer.Write(h.CellMin.x); writer.Write(h.CellMin.y); writer.Write(h.CellMin.z);
            writer.Write(h.CellMax.x); writer.Write(h.CellMax.y); writer.Write(h.CellMax.z); writer.Write(h.MassRevision);
            writer.Write(structure.NativeBytes); writer.Write(data.BrickIndex.Count); writer.Write(data.BrickIndex.Capacity);
            Write(writer, data.Palette); Write(writer, data.Instances); Write(writer, data.FreeInstances);
            Write(writer, data.Bricks); Write(writer, data.Masks); Write(writer, data.CellPages); Write(writer, data.FreePages); Write(writer, data.SparsePages); Write(writer, data.FreeSparsePages);
            for (int i = 0; i < data.Bricks.Length; i++) writer.Write(data.BrickIndex[data.Bricks[i].Coord]);
            return stream.ToArray();
        }
        static unsafe void Write<T>(BinaryWriter writer, NativeList<T> list) where T : unmanaged
        {
            writer.Write(list.Capacity); writer.Write(list.Length);
            var bytes = new byte[list.Length * UnsafeUtility.SizeOf<T>()];
            if (bytes.Length > 0) fixed (byte* target = bytes) UnsafeUtility.MemCpy(target, list.GetUnsafeReadOnlyPtr(), bytes.Length);
            writer.Write(bytes);
        }
    }
}
