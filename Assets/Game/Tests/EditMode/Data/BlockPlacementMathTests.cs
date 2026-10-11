using System.Collections.Generic;
using System.Linq;
using Game.Data.Blocks;
using Game.Data.Definitions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-006: placement maths (SPEC-04 §5.6, §6.3).</summary>
    public sealed class BlockPlacementMathTests
    {
        [TearDown] public void TearDown() => DestroyAll();

        static HashSet<int3> Cells(ref BlockDef def, ref OrientationTablesBlob ot, byte o, int3 origin)
        {
            using var list = new NativeList<int3>(16, Allocator.Temp);
            BlockPlacementMath.GetCells(ref def, ref ot, o, origin, list);
            return new HashSet<int3>(list.AsArray().ToArray());
        }

        [Test]
        public void GetCells_RotatesAroundRoot()
        {
            var blob = BlockDefsBlobBuilderTests.Build(BlockDefsBlobBuilderTests.Defs(out _, out _), out var catalog, out var modules);
            var ot = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            try
            {
                ushort cap = catalog.GetBlockIndex(catalog.Get<BlockDefinition>(new Game.Core.ContentId("block_capacitor_small")));
                ref var def = ref blob.Value.Blocks[cap];
                using var list = new NativeList<int3>(8, Allocator.Temp);
                BlockPlacementMath.GetCells(ref def, ref ot.Value, 4, new int3(10, 0, 5), list);   // yaw +90: +X → −Z
                Assert.AreEqual(new int3(10, 0, 5), list[0], "root stays at the origin");
                CollectionAssert.AreEquivalent(new[] { new int3(10, 0, 5), new int3(10, 0, 4), new int3(10, 1, 5), new int3(10, 1, 4) }, list.AsArray().ToArray());
            }
            finally { blob.Dispose(); modules.Dispose(); ot.Dispose(); }
        }

        static IEnumerable<ushort> Subjects(Game.Data.Services.ContentCatalog c)
            => new[] { "block_chiral_l", "block_chiral_r", "block_capacitor_small", "block_thruster_medium" }.Select(id => c.GetBlockIndex(c.Get<BlockDefinition>(new Game.Core.ContentId(id))));

        [Test]
        public void MirrorPlace_ReflectsCellsExactly()
        {
            var blob = BlockDefsBlobBuilderTests.Build(BlockDefsBlobBuilderTests.Defs(out _, out _), out var catalog, out var modules);
            var ot = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            try
            {
                var origin = new int3(3, -2, 5);
                foreach (var k in Subjects(catalog))
                    for (byte o = 0; o < 24; o++)
                        for (int axis = 0; axis < 3; axis++)
                            foreach (int plane in new[] { 0, 1, 7, -3 })
                            {
                                ref var def = ref blob.Value.Blocks[k];
                                BlockPlacementMath.MirrorPlace(ref def, ref ot.Value, o, origin, axis, plane, out var mb, out var mo, out var morigin);
                                var expected = new HashSet<int3>(Cells(ref def, ref ot.Value, o, origin).Select(c => { c[axis] = plane - 1 - c[axis]; return c; }));
                                var actual = Cells(ref blob.Value.Blocks[mb], ref ot.Value, mo, morigin);
                                Assert.IsTrue(expected.SetEquals(actual), $"block {k} o {o} axis {axis} plane {plane}");
                            }
            }
            finally { blob.Dispose(); modules.Dispose(); ot.Dispose(); }
        }

        [Test]
        public void MirrorPlace_Twice_ReturnsOriginal()
        {
            var blob = BlockDefsBlobBuilderTests.Build(BlockDefsBlobBuilderTests.Defs(out _, out _), out var catalog, out var modules);
            var ot = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            try
            {
                var origin = new int3(-4, 6, 1);
                foreach (var k in Subjects(catalog))
                    for (byte o = 0; o < 24; o++)
                        for (int axis = 0; axis < 3; axis++)
                        {
                            BlockPlacementMath.MirrorPlace(ref blob.Value.Blocks[k], ref ot.Value, o, origin, axis, 3, out var b1, out var o1, out var p1);
                            BlockPlacementMath.MirrorPlace(ref blob.Value.Blocks[b1], ref ot.Value, o1, p1, axis, 3, out var b2, out var o2, out var p2);
                            Assert.AreEqual(k, b2); Assert.AreEqual(o, o2); Assert.AreEqual(origin, p2, $"block {k} o {o} axis {axis}");
                        }
            }
            finally { blob.Dispose(); modules.Dispose(); ot.Dispose(); }
        }

        [Test]
        public void Chirality_NoRotationEqualsMirror()
        {
            var shape = BlockDefsBlobBuilderTests.Chiral.Select(c => new int3(c.x, c.y, c.z)).ToArray();
            var mirror = BlockDefsBlobBuilderTests.ChiralMirror.Select(c => new int3(c.x, c.y, c.z)).ToArray();
            HashSet<int3> Normalize(IEnumerable<int3> cells) { var arr = cells.ToArray(); var min = arr.Aggregate(math.min); return new HashSet<int3>(arr.Select(c => c - min)); }
            var target = Normalize(mirror);
            for (byte o = 0; o < 24; o++)
                Assert.IsFalse(target.SetEquals(Normalize(shape.Select(c => OrientationTables.Rotate(c, o)))), $"orientation {o} maps the shape onto its mirror");
        }

        [Test]
        public void WorldAttachFaces_RotatesBits()
        {
            var ot = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            try
            {
                Assert.AreEqual(BlockFaceMask.NegZ, BlockPlacementMath.WorldAttachFaces(BlockFaceMask.PosX, 4, ref ot.Value), "yaw +90: +X → −Z");
                Assert.AreEqual(BlockFaceMask.All, BlockPlacementMath.WorldAttachFaces(BlockFaceMask.All, 13, ref ot.Value));
                Assert.AreEqual(BlockFaceMask.None, BlockPlacementMath.WorldAttachFaces(BlockFaceMask.None, 7, ref ot.Value));
                for (byte o = 0; o < 24; o++)
                    for (int f = 0; f < 6; f++)
                        Assert.AreEqual((BlockFaceMask)(1 << (int)OrientationTables.RotateFace((BlockFace)f, o)), BlockPlacementMath.WorldAttachFaces((BlockFaceMask)(1 << f), o, ref ot.Value));
            }
            finally { ot.Dispose(); }
        }
    }
}
