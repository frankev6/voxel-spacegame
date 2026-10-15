using Game.Data.Blocks;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Rendering
{
    /// <summary>ADR-0115: Xeno depth storage, snapshot encoding and the surface-nets shell (LOD0 and LOD1).</summary>
    public sealed class XenoMesherTests
    {
        BlobAssetReference<BrickShapeTables> shapes;
        NativeArray<uint> snapshot;
        NativeList<PackedBrickVertex> vertices;
        NativeList<uint> indices;
        [SetUp] public void SetUp()
        {
            var orientations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Temp);
            try { shapes = BrickShapeTablesBuilder.Build(ref orientations.Value); } finally { orientations.Dispose(); }
            snapshot = new NativeArray<uint>(MeshCell.SnapCells, Allocator.TempJob);
            vertices = new NativeList<PackedBrickVertex>(20000, Allocator.TempJob); indices = new NativeList<uint>(60000, Allocator.TempJob);
        }
        [TearDown] public void TearDown() { indices.Dispose(); vertices.Dispose(); snapshot.Dispose(); shapes.Dispose(); }
        [BurstCompile(CompileSynchronously = true)] struct MeshJob : IJob
        {
            [ReadOnly] public NativeArray<uint> Snapshot;
            [ReadOnly] public BlobAssetReference<BrickShapeTables> Shapes;
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public int Lod;
            public void Execute()
            {
                if (Lod == 0) BrickMesher.MeshLod0(Snapshot, ref Shapes.Value, Vertices, Indices, out _, out _);
                else BrickMesher.MeshLod1(Snapshot, ref Shapes.Value, Vertices, Indices, out _, out _);
            }
        }
        void Mesh(int lod = 0) => new MeshJob { Snapshot = snapshot, Shapes = shapes, Vertices = vertices, Indices = indices, Lod = lod }.Run();
        static uint Tissue(float depthCells) => MeshCell.Encode(1, MeshCell.XenoShape, math.clamp((int)math.round(depthCells * 8), 0, 31), 8, 31, 0, false);
        /// <summary>A sphere of tissue centred in the brick, depth = distance below its surface.</summary>
        void Ball(float3 centre, float radius)
        {
            for (int z = -1; z <= 16; z++) for (int y = -1; y <= 16; y++) for (int x = -1; x <= 16; x++)
            {
                float d = math.distance(new float3(x, y, z) + .5f, centre) - radius;
                snapshot[MeshCell.SnapIndex(x, y, z)] = d < 0 ? Tissue(-d) : 0;
            }
        }

        [Test] public void Depth_RoundTripsThroughCellFlags()
        {
            byte flags = XenoDepth.With((byte)CellFlags.Scorched, 1.3f);
            Assert.AreEqual(1.3f, XenoDepth.Cells(flags), 1f / 32);
            Assert.AreEqual((byte)CellFlags.Scorched, flags & (byte)CellFlags.Scorched, "Depth never disturbs other flags.");
            Assert.AreEqual(XenoDepth.Max / 16f, XenoDepth.Cells(XenoDepth.With(0, 99)), 1e-6f, "Depth saturates instead of wrapping.");
        }
        [Test] public void SmoothVertex_PacksPositionNormalAndDepth()
        {
            var normal = math.normalize(new float3(.3f, -.8f, .52f));
            var packed = PackedBrickVertex.PackSmooth(new float3(3.37f, 15.9f, -.4f), normal, 8, 31, 9, 2);
            Assert.IsTrue(packed.IsSmooth);
            Assert.That(math.distance(packed.Position, new float3(3.37f, 15.9f, -.4f)), Is.LessThan(1f / 16 + 1e-4f));
            Assert.AreEqual(8u, (packed.Hi >> 8) & 255); Assert.AreEqual(31u, (packed.Hi >> 16) & 255);
            uint flags = packed.Hi >> 24;
            Assert.AreEqual(9u, (flags & 3) | (((flags >> 6) & 3) << 2), "Depth (quarter cells) survives in the AO and spare flag bits.");
            Assert.AreEqual(2u, (flags >> 2) & 3, "Damage keeps its usual bits.");
            Assert.IsFalse(PackedBrickVertex.Pack(2, 4, 6, 0, 0, 0, 3, 0, false).IsSmooth);
        }
        [Test] public void Ball_MeshesAClosedOutwardShellWithoutBlockFaces()
        {
            float3 centre = new float3(8, 8, 8); Ball(centre, 5.2f); Mesh();
            Assert.Greater(vertices.Length, 100); Assert.AreEqual(0, indices.Length % 3);
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.IsTrue(vertices[i].IsSmooth, "Xeno cells emit no block faces.");
                float r = math.distance(vertices[i].Position, centre);
                Assert.AreEqual(5.2f, r, .75f, "Vertices lie on the grown surface.");
            }
            int outward = 0;
            for (int t = 0; t < indices.Length; t += 3)
            {
                float3 a = vertices[(int)indices[t]].Position, b = vertices[(int)indices[t + 1]].Position, c = vertices[(int)indices[t + 2]].Position;
                if (math.dot(math.cross(b - a, c - a), (a + b + c) / 3 - centre) > 0) outward++;
            }
            Assert.AreEqual(indices.Length / 3, outward, "Every triangle faces away from the tissue.");
            // Closed: every edge is shared by exactly two triangles.
            var edges = new NativeParallelHashMap<long, int>(indices.Length, Allocator.Temp);
            for (int t = 0; t < indices.Length; t += 3) for (int e = 0; e < 3; e++)
            {
                long u = indices[t + e], v = indices[t + (e + 1) % 3]; long key = math.min(u, v) * 1000000 + math.max(u, v);
                edges.TryGetValue(key, out int n); edges[key] = n + 1;
            }
            foreach (var pair in edges) Assert.AreEqual(2, pair.Value, "Open edge in the shell");
            edges.Dispose();
        }
        [Test] public void DeepTissue_ExposedByAWound_CarriesItsDepth()
        {
            Ball(new float3(8, 8, 8), 6.5f);
            for (int z = 6; z <= 9; z++) for (int y = 11; y <= 16; y++) for (int x = 6; x <= 9; x++) snapshot[MeshCell.SnapIndex(x, y, z)] = 0;
            Mesh(); int wound = 0, shell = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                uint flags = vertices[i].Hi >> 24; float depth = ((flags & 3) | (((flags >> 6) & 3) << 2)) / 4f;
                if (depth >= 1) wound++; else shell++;
            }
            Assert.Greater(wound, 10, "The crater floor sits in deep tissue and glows."); Assert.Greater(shell, 100);
        }
        [Test] public void Crater_FloorFollowsTheRemovedCells()
        {
            // QA-001: exposed tissue keeps its spawn depth (~2 cells), so the shell regrew 0.4 cells over a crater (floor 12.375
            // for cells removed down to y = 12). Wound cells mesh at a shallow depth: the floor sits on the removed volume.
            Ball(new float3(8, 8, 8), 6.5f);
            for (int z = 7; z <= 9; z++) for (int x = 7; x <= 9; x++) for (int y = 12; y <= 14; y++) snapshot[MeshCell.SnapIndex(x, y, z)] = 0;
            Mesh(); float floor = float.MaxValue; bool glows = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = vertices[i].Position;
                if (math.abs(p.x - 8.5f) > .75f || math.abs(p.z - 8.5f) > .75f || p.y < 9) continue;
                if (p.y < floor) { floor = p.y; uint flags = vertices[i].Hi >> 24; glows = ((flags & 3) | (((flags >> 6) & 3) << 2)) >= 4; }
            }
            Assert.Less(floor, 12.1f, "The crater floor sits on the removed cells instead of regrowing over them.");
            Assert.IsTrue(glows, "The crater floor keeps its true depth, so it glows as a wound.");
        }
        [Test] public void Lod1_StillShellsTissueAndNeverAddsCubes()
        {
            Ball(new float3(8, 8, 8), 6f); Mesh(1);
            Assert.Greater(vertices.Length, 20);
            for (int i = 0; i < vertices.Length; i++) Assert.IsTrue(vertices[i].IsSmooth);
        }
        [Test] public void TissueOnlyInTheApron_EmitsNoGeometry()
        {
            // Quads on edges whose lower sample lies in the neighbour belong to that brick; no orphan vertices may remain.
            for (int z = -1; z <= 16; z++) for (int y = -1; y <= 16; y++) snapshot[MeshCell.SnapIndex(-1, y, z)] = Tissue(.4f);
            Mesh(); Assert.AreEqual(0, vertices.Length); Assert.AreEqual(0, indices.Length);
            Mesh(1); Assert.AreEqual(0, vertices.Length); Assert.AreEqual(0, indices.Length);
        }
        [Test] public void BlockOnlyBrick_HasNoSmoothVertices()
        {
            for (int z = 4; z < 8; z++) for (int y = 4; y < 8; y++) for (int x = 4; x < 8; x++) snapshot[MeshCell.SnapIndex(x, y, z)] = MeshCell.Encode(1, 0, 0, 0, 0, 0, false);
            Mesh(); Assert.Greater(vertices.Length, 0);
            for (int i = 0; i < vertices.Length; i++) Assert.IsFalse(vertices[i].IsSmooth);
        }
    }
}
