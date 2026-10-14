using Game.ShipEditor.Data;
using Game.ShipEditor.Validation;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class SymmetryMapperTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static BlockRecord Record(ushort block, int3 origin) => new() { Block = block, Origin = origin, Color = 3, DamageOffset = -1 };
        [Test] public void MirrorTwice_Identity_AllBlocksAllOrients()
        {
            using var f = new DraftAdapterTests.Fixture(); var mapper = new SymmetryMapper(f.Context);
            using var sourceDamage = new NativeList<byte>(Allocator.Temp); using var outputDamage = new NativeList<byte>(Allocator.Temp);
            for (ushort block = 1; block < f.Context.Footprints.BlockCount; block++)
            {
                ref var definition = ref f.Context.Definitions.Value.Blocks[block]; if (definition.MirrorIndex == 0) continue;
                int cells = definition.Cells.Length; sourceDamage.Clear();
                for (int i = 0; i < cells; i++) sourceDamage.Add((byte)(i == 0 && cells > 1 ? 0 : 31 + i));
                uint hp = math.asuint(definition.RequiresInstance ? definition.FunctionHp / 2 : float.NaN);
                for (int i = 0; i < 4; i++) sourceDamage.Add((byte)(hp >> (8 * i)));
                for (byte orient = 0; orient < 24; orient++)
                    for (int axis = 0; axis < 3; axis++)
                    {
                        var source = Record(block, new int3(-7, 3, 9)); source.Orient = orient; source.DamageOffset = 0; outputDamage.Clear();
                        int plane = axis - 2;
                        Assert.AreEqual(PlacementError.None, mapper.Mirror(source, sourceDamage.AsArray(), axis, plane, outputDamage, out var mirrored));
                        Assert.AreEqual(PlacementError.None, mapper.Mirror(mirrored, outputDamage.AsArray(), axis, plane, outputDamage, out var restored));
                        Assert.AreEqual(source, restored); Assert.AreEqual(source.Color, restored.Color);
                        for (int i = 0; i < sourceDamage.Length; i++) Assert.AreEqual(sourceDamage[i], outputDamage[restored.DamageOffset + i]);
                    }
            }
        }
        [Test] public void LShape_ChiralPartner()
        {
            using var f = new DraftAdapterTests.Fixture(); var mapper = new SymmetryMapper(f.Context);
            using var bytes = new NativeList<byte>(Allocator.Temp); var original = Record(f.Prop, new int3(2, -3, 5));
            Assert.AreEqual(PlacementError.None, mapper.Mirror(original, default, 0, 0, bytes, out var mirrored));
            Assert.AreEqual(f.Context.Definitions.Value.Blocks[f.Prop].MirrorIndex, mirrored.Block); Assert.AreNotEqual(original.Block, mirrored.Block);
            using var expected = new NativeHashSet<int3>(8, Allocator.Temp);
            foreach (var offset in f.Context.Footprints.Get(original.Block, original.Orient).Cells)
            { var cell = original.Origin + offset; cell.x = -cell.x - 1; expected.Add(cell); }
            foreach (var offset in f.Context.Footprints.Get(mirrored.Block, mirrored.Orient).Cells)
                Assert.IsTrue(expected.Contains(mirrored.Origin + offset));
        }
        [Test] public void OddPlane_CentreCellSelf()
        {
            using var f = new DraftAdapterTests.Fixture(); var mapper = new SymmetryMapper(f.Context);
            using var bytes = new NativeList<byte>(Allocator.Temp); var original = Record(f.Hull, new int3(2, 0, 0));
            Assert.AreEqual(PlacementError.None, mapper.Mirror(original, default, 0, 5, bytes, out var mirrored)); Assert.AreEqual(original, mirrored);
            var settings = new SymmetrySettings(); mapper.SetEnabled(ref settings, 0, true, original);
            Assert.AreEqual(5, settings.PlaneX2); Assert.AreEqual(1, settings.Version);
            mapper.SetEnabled(ref settings, 0, true, original); Assert.AreEqual(1, settings.Version);
            SymmetryMapper.Nudge(ref settings, 0, 1); Assert.AreEqual(6, settings.PlaneX2); Assert.AreEqual(2, settings.Version);
            Assert.AreEqual(1, mapper.DefaultPlane(2, null));
        }
        [Test] public void EvenPlane_NoSelf()
        {
            using var f = new DraftAdapterTests.Fixture(); var mapper = new SymmetryMapper(f.Context);
            using var bytes = new NativeList<byte>(Allocator.Temp); var original = Record(f.Hull, int3.zero);
            Assert.AreEqual(PlacementError.None, mapper.Mirror(original, default, 0, 0, bytes, out var mirrored)); Assert.AreEqual(new int3(-1, 0, 0), mirrored.Origin);
            Assert.AreEqual(2, mapper.DefaultPlane(0, Record(f.Multi, int3.zero)), "Two-wide footprint defaults to a boundary plane.");
        }
        [Test] public void ThreePlanes_EightCopiesDeduped()
        {
            using var f = new DraftAdapterTests.Fixture(); var mapper = new SymmetryMapper(f.Context);
            using var records = new NativeList<BlockRecord>(Allocator.Temp); using var bytes = new NativeList<byte>(Allocator.Temp);
            var settings = new SymmetrySettings { MirrorX = true, MirrorY = true, MirrorZ = true };
            var original = Record(f.Hull, new int3(2, 3, 4));
            Assert.AreEqual(PlacementError.None, mapper.BuildCopies(original, default, settings, records, bytes)); Assert.AreEqual(8, records.Length);
            int[] masks = { 0, 1, 2, 4, 3, 5, 6, 7 };
            for (int i = 0; i < masks.Length; i++)
            {
                var expected = original.Origin;
                for (int axis = 0; axis < 3; axis++) if ((masks[i] & (1 << axis)) != 0) expected[axis] = -expected[axis] - 1;
                Assert.AreEqual(expected, records[i].Origin);
            }
            records.Clear(); settings.PlaneX2 = 5;
            Assert.AreEqual(PlacementError.None, mapper.BuildCopies(original, default, settings, records, bytes)); Assert.AreEqual(4, records.Length);
        }
        [Test] public void MirroredRemove_DifferentBlock_Skipped()
        {
            using var f = new DraftAdapterTests.Fixture(); var mapper = new SymmetryMapper(f.Context);
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(1, 0, 0), Color = 3 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Single, Cell = new int3(-2, 0, 0) });
            using var records = new NativeList<BlockRecord>(Allocator.Temp);
            var settings = new SymmetrySettings { MirrorX = true };
            Assert.AreEqual(PlacementError.None, mapper.BuildRemovals(Record(f.Hull, new int3(1, 0, 0)), settings, f.Draft, records));
            Assert.AreEqual(1, records.Length); Assert.AreEqual(f.Hull, records[0].Block);
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.RemoveBlock, Block = f.Single, Cell = new int3(-2, 0, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(-2, 0, 0), Color = 9 }); records.Clear();
            Assert.AreEqual(PlacementError.None, mapper.BuildRemovals(Record(f.Hull, new int3(1, 0, 0)), settings, f.Draft, records));
            Assert.AreEqual(2, records.Length); Assert.AreEqual(9, records[1].Color, "Removal uses the matched block's actual colour identity.");
        }
        [Test] public void UnsupportedPair_GroupRejected()
        {
            using var f = new DraftAdapterTests.Fixture(); var mapper = new SymmetryMapper(f.Context);
            // This fixture owns its blob: deliberately model malformed external content without editing shared assets.
            ref var broken = ref f.Context.Definitions.Value.Blocks[f.Prop]; broken.MirrorIndex = f.Hull;
            using var records = new NativeList<BlockRecord>(Allocator.Temp); using var bytes = new NativeList<byte>(Allocator.Temp);
            var sentinel = Record(f.Hull, new int3(99)); records.Add(sentinel); bytes.Add(77);
            Assert.AreEqual(PlacementError.SymmetryUnsupported, mapper.BuildCopies(Record(f.Prop, int3.zero), default,
                new SymmetrySettings { MirrorX = true }, records, bytes));
            Assert.AreEqual(1, records.Length); Assert.AreEqual(sentinel, records[0]); Assert.AreEqual(1, bytes.Length); Assert.AreEqual(77, bytes[0]);
        }
    }
}
