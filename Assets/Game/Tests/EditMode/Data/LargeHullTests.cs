using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Data;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using NUnit.Framework;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.Data
{
    /// <summary>DATA-013 (ADR-0121): large hull blocks are full cube boxes placed, kept and removed as one block instance.</summary>
    public sealed class LargeHullTests
    {
        [TearDown] public void TearDown() => DestroyAll();

        static Vector3Int[] Box(int sx, int sy, int sz, bool hollow = false)
        {
            var cells = new List<Vector3Int>();
            for (int z = 0; z < sz; z++) for (int y = 0; y < sy; y++) for (int x = 0; x < sx; x++)
                if (!(hollow && x == 1 && y == 1 && z == 1)) cells.Add(new Vector3Int(x, y, z));
            return cells.ToArray();
        }
        static bool E036(BlockDefinition block) => Validate(block).Entries.Any(e => e.Code == "DATA-E036");

        [Test] public void Validation_BoxesOnly()
        {
            Assert.IsFalse(E036(Block("block_box2", BlockKind.Structural, Box(2, 2, 2))), "2×2×2");
            Assert.IsFalse(E036(Block("block_box3", BlockKind.Structural, Box(3, 3, 3))), "3×3×3");
            Assert.IsTrue(E036(Block("block_box223", BlockKind.Structural, Box(2, 2, 3))), "2×2×3 is not a cube");
            Assert.IsTrue(E036(Block("block_hollow", BlockKind.Structural, Box(3, 3, 3, hollow: true))), "a hollow box is not full");
            Assert.IsTrue(E036(Block("block_box4", BlockKind.Structural, Box(4, 4, 4))), "only sides 2 and 3");
            var shifted = Box(2, 2, 2).Select(c => c - Vector3Int.one).ToArray();
            Assert.AreEqual(0, BlockDefinition.LargeHullSide(shifted), "rooted at its corner");
            var wedged = Block("block_wedged", BlockKind.Structural, Box(2, 2, 2), edit: so =>
            {
                var p = so.FindProperty("shapes"); p.arraySize = 8;
                for (int i = 0; i < 8; i++) p.GetArrayElementAtIndex(i).intValue = (int)(i == 7 ? BlockShape.Wedge : BlockShape.Cube);
            });
            Assert.IsTrue(E036(wedged), "every cell a cube");
        }

        static ContentService Shipped() => new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));

        [Test] public void Instance_OneBlockManyCells()
        {
            using var content = Shipped();
            var voxel = new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            var hull = content.Catalog.Get<BlockDefinition>(new ContentId("block_hull"));
            foreach (int side in new[] { 2, 3 })
            {
                var asset = content.Catalog.Get<BlockDefinition>(new ContentId("block_hull_large_" + side));
                Assert.AreEqual(0, Validate(asset).ErrorCount, asset.IdString);
                ushort index = content.Catalog.GetBlockIndex(asset);
                ref var d = ref content.BlockDefs.Value.Blocks[index];
                Assert.AreEqual(side * side * side, d.Cells.Length); Assert.IsTrue(d.RequiresInstance);
                Assert.AreEqual(hull.CellHp, d.CellHp); Assert.AreEqual(hull.CellMass, d.CellMass);
                Assert.AreEqual(OrientationMask.AllBits, d.AllowedOrientations);
                for (byte orient = 0; orient < 24; orient++)
                {
                    using var s = new VoxelStructure(.5f);
                    Assert.AreEqual(PlaceResult.Ok, s.TryPlaceBlock(new BlockPlacement { Block = index, Origin = new int3(4, 4, 4), Orient = orient }, voxel), $"{asset.IdString} orient {orient}");
                    Assert.AreEqual(side * side * side, s.SolidCellCount);
                    Assert.AreEqual(1, s.Data.Instances.Length - s.Data.FreeInstances.Length, "one instance for every cell");
                    // Any of its cells selects and removes the whole block.
                    var cell = new int3(4, 4, 4);
                    Assert.IsTrue(s.TryGetBlockAt(cell, out var found, out _)); Assert.AreEqual(index, found.Block);
                    Assert.IsTrue(s.TryRemoveBlockAt(cell, voxel, out _));
                    Assert.AreEqual(0, s.SolidCellCount, "removed as one block");
                }
            }
        }
    }
}
