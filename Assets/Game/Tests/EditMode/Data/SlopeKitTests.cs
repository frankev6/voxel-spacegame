using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    public sealed class SlopeKitTests
    {
        [TearDown] public void TearDown() => DestroyAll();
        static void Shapes(SerializedObject so, params BlockShape[] shapes)
        {
            var p = so.FindProperty("shapes"); p.arraySize = shapes.Length;
            for (int i = 0; i < shapes.Length; i++) p.GetArrayElementAtIndex(i).intValue = (int)shapes[i];
        }
        static BlockDefinition LongSlope(bool reversed = false) => Block("block_long", BlockKind.Structural,
            reversed ? new[] { Vector3Int.up, Vector3Int.zero } : new[] { Vector3Int.zero, Vector3Int.up }, edit: so =>
            {
                Set(so, "shape", (int)BlockShape.LongSlopeHigh);
                Shapes(so, reversed ? BlockShape.LongSlopeLow : BlockShape.LongSlopeHigh, reversed ? BlockShape.LongSlopeHigh : BlockShape.LongSlopeLow);
            });
        static void Error(BlockDefinition block, string code)
        { Assert.IsTrue(Validate(block).Entries.Any(e => e.Code == code), code); }
        [Test] public void Validation_ApprovedKitOnly()
        {
            Assert.AreEqual(0, Validate(LongSlope()).ErrorCount);
            Assert.AreEqual(0, Validate(LongSlope(true)).ErrorCount);
            var single = Block("block_single", BlockKind.Structural);
            foreach (var shape in new[] { BlockShape.Cube, BlockShape.Wedge, BlockShape.HipCorner })
            { Edit(single, so => Shapes(so, shape)); Assert.AreEqual(0, Validate(single).ErrorCount); }
            Edit(single, so => Shapes(so, BlockShape.LongSlopeLow)); Error(single, "DATA-E036");
            Edit(single, so => Shapes(so, (BlockShape)5)); Error(single, "DATA-E050");
            Edit(single, so => Shapes(so, BlockShape.Cube, BlockShape.Cube)); Error(single, "DATA-E051");
            var prop = Block("block_prop", BlockKind.Prop, edit: so => Shapes(so, BlockShape.Cube)); Error(prop, "DATA-E052");
            var slope = LongSlope(); Edit(slope, so => Shapes(so)); Error(slope, "DATA-E036");
            Edit(slope, so => Shapes(so, BlockShape.LongSlopeLow, BlockShape.LongSlopeHigh)); Error(slope, "DATA-E036");
            Error(Block("block_large", BlockKind.Structural, new[] { Vector3Int.zero, Vector3Int.right }), "DATA-E036");
        }
        [Test] public void Bake_CanonicalShapeMappingAndHash()
        {
            var slope = LongSlope(true);
            var single = Block("block_single", BlockKind.Structural, edit: so => Set(so, "shape", (int)BlockShape.Wedge));
            var defs = new List<ContentDefinition> { Block("block_missing", BlockKind.Structural), Item("item_missing"), slope, single };
            var a = BlockDefsBlobBuilderTests.Build(defs, out var catalog, out var ma);
            try
            {
                ref var d = ref a.Value.Blocks[catalog.GetBlockIndex(slope)];
                Assert.AreEqual(int3.zero, d.Cells[0]); Assert.AreEqual(new int3(0,1,0), d.Cells[1]);
                Assert.AreEqual(BlockShape.LongSlopeHigh, d.GetShape(0)); Assert.AreEqual(BlockShape.LongSlopeLow, d.GetShape(1));
                Assert.AreEqual(BlockShape.LongSlopeHigh, d.Shape); Assert.IsTrue(d.RequiresInstance);
                Assert.AreEqual(BlockShape.Wedge, a.Value.Blocks[catalog.GetBlockIndex(single)].GetShape(0));
                Assert.AreEqual(0, a.Value.Blocks[0].Shapes.Length);
                Edit(slope, so => { Set(so, "cells", new[] { Vector3Int.zero, Vector3Int.up }); Shapes(so, BlockShape.LongSlopeHigh, BlockShape.LongSlopeLow); });
                Edit(single, so => Shapes(so, BlockShape.Wedge));
                var b = BlockDefsBlobBuilderTests.Build(defs, out _, out var mb);
                try { Assert.AreEqual(a.Value.BlockDefsHash, b.Value.BlockDefsHash, "authoring order and equivalent explicit/default shapes do not change meaning"); }
                finally { b.Dispose(); mb.Dispose(); }
                Edit(single, so => Shapes(so, BlockShape.HipCorner));
                var c = BlockDefsBlobBuilderTests.Build(defs, out _, out var mc);
                try
                {
                    Assert.AreNotEqual(a.Value.BlockDefsHash, c.Value.BlockDefsHash);
                    ref var changed = ref c.Value.Blocks[catalog.GetBlockIndex(single)];
                    Assert.AreEqual(BlockShape.HipCorner, changed.Shape); Assert.AreEqual(changed.Shape, changed.GetShape(0));
                }
                finally { c.Dispose(); mc.Dispose(); }
            }
            finally { a.Dispose(); ma.Dispose(); }
        }
        [Test] public void ShippedKit_ValuesAndInstances()
        {
            using var content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            var icon = content.Catalog.Get<BlockDefinition>(new ContentId("block_hull_angled")).Icon;
            foreach (string id in new[] { "block_hull_hip", "block_hull_slope_long" })
            {
                var asset = content.Catalog.Get<BlockDefinition>(new ContentId(id)); Assert.IsNotNull(asset);
                Assert.AreEqual(0, Validate(asset).ErrorCount); Assert.AreSame(icon, asset.Icon);
                ref var d = ref content.BlockDefs.Value.Blocks[content.Catalog.GetBlockIndex(asset)];
                bool longSlope = id.EndsWith("_long");
                Assert.AreEqual(longSlope ? 2 : 1, d.Cells.Length); Assert.AreEqual(d.Cells.Length, d.Shapes.Length);
                Assert.AreEqual(longSlope, d.RequiresInstance); Assert.AreEqual(longSlope, asset.RequiresInstance);
                Assert.AreEqual(longSlope ? BlockShape.LongSlopeHigh : BlockShape.HipCorner, d.Shape);
                if (longSlope) { Assert.AreEqual(new int3(0,1,0), d.Cells[1]); Assert.AreEqual(BlockShape.LongSlopeLow, d.GetShape(1)); }
                Assert.AreEqual(50f, d.CellHp); Assert.AreEqual(15f, d.CellMass); Assert.AreEqual(0f, d.FunctionHp);
                Assert.AreEqual(0UL, d.ModuleMask); Assert.AreEqual(0, d.MaterialLayer); Assert.AreEqual(0, d.ArmorClass);
                Assert.AreEqual(OrientationMask.AllBits, d.AllowedOrientations); Assert.AreEqual(BlockFaceMask.All, d.AttachFaces);
                Assert.AreEqual(longSlope ? 8 : 4, asset.BasePrice); Assert.AreEqual(-1, d.PropMeshIndex);
            }
        }
    }
}
