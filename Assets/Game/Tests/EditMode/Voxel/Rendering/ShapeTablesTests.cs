using System.Collections.Generic;
using Game.Data.Blocks;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    [Category("Voxel")]
    public sealed class ShapeTablesTests
    {
        BlobAssetReference<OrientationTablesBlob> orientations;
        BlobAssetReference<BrickShapeTables> shapes;
        [SetUp] public void SetUp()
        {
            orientations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
            shapes = BrickShapeTablesBuilder.Build(ref orientations.Value);
        }
        [TearDown] public void TearDown() { if (shapes.IsCreated) shapes.Dispose(); if (orientations.IsCreated) orientations.Dispose(); }
        static int3 Vertex(ShapeFace f, int i) => i == 0 ? f.V0 : i == 1 ? f.V1 : i == 2 ? f.V2 : f.V3;
        static bool Inside(int shape, int3 p)
        {
            if (math.any(p < 0) || math.any(p > 2)) return false;
            return shape switch
            {
                1 => p.y + p.z <= 2, 2 => p.y + p.z <= 2 && p.x + p.z <= 2,
                3 => p.y + 2*p.z <= 4, 4 => p.y + 2*p.z <= 2, _ => true
            };
        }
        [Test] public void Winding_AllShapesAllOrients()
        {
            ref var table = ref shapes.Value;
            Assert.AreEqual(120, table.FirstFace.Length); Assert.AreEqual(648, table.Faces.Length);
            int[] counts = { 6, 5, 5, 6, 5 };
            for (int shape = 0; shape < 5; shape++) for (byte o = 0; o < 24; o++)
            {
                int index = shape * 24 + o; Assert.AreEqual(counts[shape], table.FaceCount[index]); bool half = false;
                for (int j = 0; j < table.FaceCount[index]; j++)
                {
                    var face = table.Faces[table.FirstFace[index] + j]; var normal = VoxelNormals.Normal(face.Normal);
                    float3 cross = math.cross((float3)(face.V1 - face.V0), (float3)(face.V2 - face.V0));
                    Assert.Greater(math.dot(cross, normal), 0, $"shape {shape}, orientation {o}, face {j}");
                    for (int k = 0; k < face.VertexCount; k++)
                    {
                        int3 vertex = Vertex(face, k); Assert.Less(math.abs(math.dot((float3)(vertex - face.V0), normal)), 1e-6f);
                        int3 original = orientations.Value.Rotate(vertex - 1, orientations.Value.Inverse(o)) + 1;
                        Assert.IsTrue(Inside(shape, original)); half |= math.any(original == 1);
                        if (face.Plane != 255)
                        {
                            int axis = face.Plane / 2, coordinate = (face.Plane & 1) == 0 ? 2 : 0;
                            Assert.AreEqual(coordinate, vertex[axis]); Assert.AreEqual(face.Plane, face.Normal);
                        }
                    }
                    if (face.IsFull != 0)
                    {
                        Assert.AreEqual(4, face.VertexCount); Assert.Less(face.Plane, 6);
                        float area = .5f * (math.length(cross) + math.length(math.cross((float3)(face.V2 - face.V0), (float3)(face.V3 - face.V0))));
                        Assert.AreEqual(4f, area, 1e-6, "a full side has area four in half-cell units");
                    }
                }
                Assert.AreEqual(shape >= 3, half, "long slopes preserve their half-cell vertices exactly");
            }
        }
        static int Compare(int3 a, int3 b)
        { int x = a.x.CompareTo(b.x); if (x != 0) return x; int y = a.y.CompareTo(b.y); return y != 0 ? y : a.z.CompareTo(b.z); }
        [Test] public void ClosedSurface()
        {
            ref var table = ref shapes.Value;
            for (int index = 0; index < 120; index++)
            {
                var edges = new Dictionary<(int3, int3), (int Count, int Direction)>();
                for (int j = 0; j < table.FaceCount[index]; j++)
                {
                    var face = table.Faces[table.FirstFace[index] + j];
                    for (int k = 0; k < face.VertexCount; k++)
                    {
                        int3 a = Vertex(face, k), b = Vertex(face, (k + 1) % face.VertexCount); bool forward = Compare(a,b) < 0;
                        var key = forward ? (a,b) : (b,a); edges.TryGetValue(key, out var previous);
                        edges[key] = (previous.Count + 1, previous.Direction + (forward ? 1 : -1));
                    }
                }
                foreach (var edge in edges.Values) { Assert.AreEqual(2, edge.Count); Assert.AreEqual(0, edge.Direction); }
            }
        }
        [Test] public void FullFaces_MatchRotation()
        {
            byte[] canonical = { 63, (byte)(BlockFaceMask.NegY | BlockFaceMask.NegZ), (byte)BlockFaceMask.NegZ,
                (byte)(BlockFaceMask.NegY | BlockFaceMask.NegZ), (byte)BlockFaceMask.NegZ };
            for (int shape = 0; shape < 5; shape++) for (byte o = 0; o < 24; o++)
            {
                int expected = 0;
                for (int f = 0; f < 6; f++) if ((canonical[shape] & (1 << f)) != 0) expected |= 1 << (int)orientations.Value.RotateFace((BlockFace)f, o);
                Assert.AreEqual(expected, shapes.Value.FullFaces[shape * 24 + o]);
            }
        }
    }
}
