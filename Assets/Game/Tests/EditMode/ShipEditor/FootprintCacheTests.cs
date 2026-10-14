using Game.Data.Blocks;
using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class FootprintCacheTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        [Test] public void AllOrientations_CountPreserved_ContainsOrigin()
        {
            var blob = BlockDefsBlobBuilderTests.Build(BlockDefsBlobBuilderTests.Defs(out _, out _), out _, out var modules);
            var rotations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            try
            {
                using var cache = new FootprintCache(blob, rotations);
                for (int b = 1; b < blob.Value.Blocks.Length; b++)
                    for (byte o = 0; o < 24; o++)
                    {
                        var f = cache.Get(b, o); ref var source = ref blob.Value.Blocks[b];
                        Assert.AreEqual(source.Cells.Length, f.Cells.Length); bool origin = false;
                        for (int i = 0; i < f.Cells.Length; i++)
                        {
                            origin |= f.Cells[i].Equals(int3.zero);
                            Assert.AreEqual(source.Cells[i], OrientationTables.Rotate(f.Cells[i], OrientationTables.Inverse(o)));
                            Assert.IsTrue(math.all(f.Cells[i] >= f.Min) && math.all(f.Cells[i] <= f.Max));
                        }
                        Assert.IsTrue(origin); Assert.AreEqual(source.Clearance.Length, f.Clearance.Length);
                        for (int i = 0; i < f.Clearance.Length; i++)
                            Assert.AreEqual(source.Clearance[i], OrientationTables.Rotate(f.Clearance[i], OrientationTables.Inverse(o)));
                    }
            }
            finally { rotations.Dispose(); modules.Dispose(); blob.Dispose(); }
        }
        [Test] public void AttachMask_RotateInverse_Identity()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlockDefsBlob>(); var defs = builder.Allocate(ref root.Blocks, 64);
            for (int mask = 0; mask < 64; mask++) defs[mask].AttachFaces = (BlockFaceMask)mask;
            var blob = builder.CreateBlobAssetReference<BlockDefsBlob>(Allocator.Persistent);
            var rotations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            try
            {
                using var cache = new FootprintCache(blob, rotations);
                for (int mask = 0; mask < 64; mask++) for (byte o = 0; o < 24; o++)
                {
                    var rotated = cache.Get(mask, o).AttachFaces; int restored = 0;
                    for (int face = 0; face < 6; face++) if (((int)rotated & (1 << face)) != 0)
                        restored |= 1 << (int)OrientationTables.RotateFace((BlockFace)face, OrientationTables.Inverse(o));
                    Assert.AreEqual(mask, restored);
                }
            }
            finally { rotations.Dispose(); blob.Dispose(); }
        }
        [Test] public void LShape_Yaw90_ExpectedCells()
        {
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BlockDefsBlob>(); var defs = builder.Allocate(ref root.Blocks, 1);
            var cells = builder.Allocate(ref defs[0].Cells, 3);
            cells[0] = int3.zero; cells[1] = new int3(1, 0, 0); cells[2] = new int3(1, 0, 1);
            var clearance = builder.Allocate(ref defs[0].Clearance, 1); clearance[0] = new int3(2, 0, 0);
            var blob = builder.CreateBlobAssetReference<BlockDefsBlob>(Allocator.Persistent);
            var rotations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            try
            {
                using var cache = new FootprintCache(blob, rotations); var f = cache.Get(0, OrientationTables.AxisTurn(1, 1));
                Assert.AreEqual(int3.zero, f.Cells[0]); Assert.AreEqual(new int3(0, 0, -1), f.Cells[1]);
                Assert.AreEqual(new int3(1, 0, -1), f.Cells[2]); Assert.AreEqual(new int3(0, 0, -2), f.Clearance[0]);
                Assert.AreEqual(new int3(0, 0, -1), f.Min); Assert.AreEqual(new int3(1, 0, 0), f.Max);
            }
            finally { rotations.Dispose(); blob.Dispose(); }
        }
    }
}
