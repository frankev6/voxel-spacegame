using Game.Core;
using Game.Data;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using UnityEditor;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    /// <summary>DATA-013 (ADR-0121 decision 3): a large hull block's cells carry their place in the block, so its faces wear at the
    /// block's edges (one plate per block), in every orientation.</summary>
    public sealed class LargeHullPanelTests
    {
        [Test] public void Encode_RoundTrips()
        {
            uint c = MeshCell.Encode(1, 0, 17, 5, 9, 1, false);
            for (int side = 2; side <= 3; side++)
                for (int z = 0; z < side; z++) for (int y = 0; y < side; y++) for (int x = 0; x < side; x++)
                {
                    uint large = MeshCell.WithLarge(c, side, new int3(x, y, z));
                    Assert.AreEqual(side, MeshCell.LargeSide(large)); Assert.AreEqual(new int3(x, y, z), MeshCell.LargeOffset(large));
                    Assert.AreEqual(0, MeshCell.Orient(large), "a cube's tables use orient 0");
                    Assert.AreEqual(MeshCell.Layer(c), MeshCell.Layer(large)); Assert.AreEqual(MeshCell.Colour(c), MeshCell.Colour(large));
                    Assert.AreEqual(MeshCell.Damage(c), MeshCell.Damage(large));
                }
            Assert.AreEqual(0, MeshCell.LargeSide(c)); Assert.AreEqual(17, MeshCell.Orient(c), "ordinary cells keep their orient");
        }

        [Test] public void Faces_SpanTheBlock()
        {
            using var content = new ContentService(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/Game/Content/ContentDatabase.asset"));
            var voxel = new VoxelContent { Blocks = content.BlockDefs, Orientations = content.Orientations };
            ushort large = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull_large_3")));
            ushort hull = content.Catalog.GetBlockIndex(content.Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
            var shapes = BrickShapeTablesBuilder.Build(ref content.Orientations.Value);
            var snapshot = new NativeArray<uint>(MeshCell.SnapCells, Allocator.Temp);
            var vertices = new NativeList<PackedBrickVertex>(4096, Allocator.Temp); var indices = new NativeList<uint>(8192, Allocator.Temp);
            try
            {
                for (byte orient = 0; orient < 24; orient++)
                {
                    using var s = new VoxelStructure(.5f);
                    Assert.AreEqual(PlaceResult.Ok, s.TryPlaceBlock(new BlockPlacement { Block = large, Origin = new int3(6, 6, 6), Orient = orient }, voxel));
                    Assert.AreEqual(PlaceResult.Ok, s.TryPlaceBlock(new BlockPlacement { Block = hull, Origin = new int3(12, 12, 12) }, voxel));
                    BrickSnapshotUtility.FromStructure(s.Data, int3.zero, ref content.BlockDefs.Value, ref content.Orientations.Value, snapshot);
                    // Every cell of the block knows its offset from the box corner, whatever the orientation.
                    int3 boxMin = new int3(int.MaxValue);
                    for (int z = 0; z < 16; z++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                        if (MeshCell.LargeSide(snapshot[MeshCell.SnapIndex(x, y, z)]) == 3) boxMin = math.min(boxMin, new int3(x, y, z));
                    int cells = 0;
                    for (int z = 0; z < 16; z++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
                    {
                        uint v = snapshot[MeshCell.SnapIndex(x, y, z)];
                        if (MeshCell.LargeSide(v) != 3) continue;
                        cells++; Assert.AreEqual(new int3(x, y, z) - boxMin, MeshCell.LargeOffset(v), $"orient {orient} cell {x},{y},{z}");
                    }
                    Assert.AreEqual(27, cells);
                    Assert.AreEqual(0, MeshCell.LargeSide(snapshot[MeshCell.SnapIndex(12, 12, 12)]), "a plain hull cell is not large");

                    BrickMesher.MeshLod0(snapshot, ref shapes.Value, vertices, indices, out _, out _);
                    int panelVertices = 0, plain = 0; int2 lo = int.MaxValue, hi = int.MinValue;
                    foreach (var p in vertices)
                    {
                        int panel = (int)(p.Lo >> 24);
                        if (panel == 0) { plain++; continue; }
                        panelVertices++;
                        Assert.AreEqual(2, panel >> 6, "side 3 is stored as 2");
                        int2 rel = new int2(panel & 7, (panel >> 3) & 7); lo = math.min(lo, rel); hi = math.max(hi, rel);
                    }
                    Assert.Greater(panelVertices, 0); Assert.AreEqual(new int2(0), lo); Assert.AreEqual(new int2(6), hi, "face positions run corner to corner in half cells");
                    Assert.AreEqual((6 * 9 + 6) * 2 * 3, indices.Length, "54 faces of the block and 6 of the hull cell");
                }
            }
            finally { shapes.Dispose(); snapshot.Dispose(); vertices.Dispose(); indices.Dispose(); }
        }
    }
}
