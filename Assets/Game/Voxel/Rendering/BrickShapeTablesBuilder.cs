using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Rendering
{
    /// <summary>Builds the five approved convex shapes in all 24 proper rotations. Half-cell integer vertices keep long slopes exact.</summary>
    public static class BrickShapeTablesBuilder
    {
        public static BlobAssetReference<BrickShapeTables> Build(ref OrientationTablesBlob orientations)
        {
            var canonical = Canonical(); int total = 0;
            foreach (var shape in canonical) total += shape.Length * 24;
            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<BrickShapeTables>();
            var faces = builder.Allocate(ref root.Faces, total);
            var first = builder.Allocate(ref root.FirstFace, canonical.Length * 24);
            var count = builder.Allocate(ref root.FaceCount, canonical.Length * 24);
            var full = builder.Allocate(ref root.FullFaces, canonical.Length * 24);
            int next = 0;
            for (int shape = 0; shape < canonical.Length; shape++) for (byte orientation = 0; orientation < 24; orientation++)
            {
                int index = shape * 24 + orientation; first[index] = (ushort)next; count[index] = (byte)canonical[shape].Length;
                byte mask = 0;
                foreach (var original in canonical[shape])
                {
                    var face = original; var rotation = orientations.Matrices[orientation];
                    face.V0 = math.mul(rotation, original.V0 - 1) + 1;
                    face.V1 = math.mul(rotation, original.V1 - 1) + 1;
                    face.V2 = math.mul(rotation, original.V2 - 1) + 1;
                    face.V3 = face.VertexCount == 4 ? math.mul(rotation, original.V3 - 1) + 1 : default;
                    face.Normal = (byte)VoxelNormals.IndexOf(math.mul(rotation, VoxelNormals.Direction(original.Normal)));
                    if (face.Plane != 255) face.Plane = (byte)orientations.RotateFace((BlockFace)face.Plane, orientation);
                    if (math.dot(math.cross((float3)(face.V1 - face.V0), (float3)(face.V2 - face.V0)), VoxelNormals.Normal(face.Normal)) < 0)
                    {
                        var second = face.V1;
                        if (face.VertexCount == 4) { face.V1 = face.V3; face.V3 = second; }
                        else { face.V1 = face.V2; face.V2 = second; }
                    }
                    if (face.IsFull != 0) mask |= (byte)(1 << face.Plane);
                    faces[next++] = face;
                }
                full[index] = mask;
            }
            return builder.CreateBlobAssetReference<BrickShapeTables>(Allocator.Persistent);
        }
        static ShapeFace Face(int3 normal, byte plane, bool full, int3 a, int3 b, int3 c, int3? d = null)
            => new ShapeFace { Normal = (byte)VoxelNormals.IndexOf(normal), Plane = plane, IsFull = full ? (byte)1 : (byte)0,
                VertexCount = d.HasValue ? (byte)4 : (byte)3, V0 = a, V1 = b, V2 = c, V3 = d.GetValueOrDefault() };
        static ShapeFace Side(BlockFace side, bool full, int3 a, int3 b, int3 c, int3? d = null)
            => Face(BrickMath.FaceDirection(side), (byte)side, full, a, b, c, d);
        static ShapeFace[][] Canonical()
        {
            var a = new int3(0, 0, 0); var b = new int3(2, 0, 0); var c = new int3(2, 2, 0); var d = new int3(0, 2, 0);
            var e = new int3(0, 0, 2); var f = new int3(2, 0, 2); var g = new int3(2, 2, 2); var h = new int3(0, 2, 2);
            var eh = new int3(0, 0, 1); var fh = new int3(2, 0, 1); var gh = new int3(2, 2, 1); var hh = new int3(0, 2, 1);
            return new[]
            {
                new[] // Cube
                {
                    Side(BlockFace.PosX, true, b,c,g,f), Side(BlockFace.NegX, true, a,e,h,d),
                    Side(BlockFace.PosY, true, d,h,g,c), Side(BlockFace.NegY, true, a,b,f,e),
                    Side(BlockFace.PosZ, true, e,f,g,h), Side(BlockFace.NegZ, true, a,d,c,b)
                },
                new[] // Wedge: y + z <= 2
                {
                    Side(BlockFace.NegY, true, a,b,f,e), Side(BlockFace.NegZ, true, a,d,c,b),
                    Side(BlockFace.NegX, false, a,e,d), Side(BlockFace.PosX, false, b,c,f),
                    Face(new int3(0,1,1), 255, false, d,e,f,c)
                },
                new[] // HipCorner: y + z <= 2 and x + z <= 2
                {
                    Side(BlockFace.NegZ, true, a,d,c,b), Side(BlockFace.NegX, false, a,e,d),
                    Side(BlockFace.NegY, false, a,b,e), Face(new int3(0,1,1), 255, false, d,e,c),
                    Face(new int3(1,0,1), 255, false, b,c,e)
                },
                new[] // LongSlopeHigh: y + 2z <= 4
                {
                    Side(BlockFace.NegY, true, a,b,f,e), Side(BlockFace.NegZ, true, a,d,c,b),
                    Side(BlockFace.PosX, false, b,c,gh,f), Side(BlockFace.NegX, false, a,e,hh,d),
                    Side(BlockFace.PosY, false, d,hh,gh,c), Face(new int3(0,1,2), 255, false, e,f,gh,hh)
                },
                new[] // LongSlopeLow: y + 2z <= 2
                {
                    Side(BlockFace.NegZ, true, a,d,c,b), Side(BlockFace.NegY, false, a,b,fh,eh),
                    Side(BlockFace.NegX, false, a,eh,d), Side(BlockFace.PosX, false, b,c,fh),
                    Face(new int3(0,1,2), 255, false, d,eh,fh,c)
                }
            };
        }
    }
}
