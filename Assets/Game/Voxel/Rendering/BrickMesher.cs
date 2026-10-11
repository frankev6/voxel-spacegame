using Game.Data.Blocks;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.2: row-mask culling, half-cell AO and per-slice vertex welding.</summary>
    public static class BrickMesher
    {
        const int Rows = 18 * 18;
        const uint Interior = 0x1FFFEu; // snapshot x bits 1..16
        struct Output
        {
            public NativeList<PackedBrickVertex> Vertices;
            public NativeList<uint> Indices;
            public float3 Min, Max;
            public uint Add(PackedBrickVertex vertex)
            {
                uint index = (uint)Vertices.Length; Vertices.Add(vertex);
                float3 position = new float3(vertex.Lo & 255, (vertex.Lo >> 8) & 255, (vertex.Lo >> 16) & 255) * .5f;
                Min = math.min(Min, position); Max = math.max(Max, position); return index;
            }
        }
        public static void MeshLod0(NativeArray<uint> snapshot, ref BrickShapeTables shapes, NativeList<PackedBrickVertex> vertices,
            NativeList<uint> indices, out float3 min, out float3 max)
        {
            vertices.Clear(); indices.Clear();
            var output = new Output { Vertices = vertices, Indices = indices, Min = new float3(float.PositiveInfinity), Max = new float3(float.NegativeInfinity) };
            // Row planes: solid, structural, six full-side masks, six polygon-presence masks.
            using var masks = new NativeArray<uint>(Rows * 14, Allocator.Temp);
            BuildMasks(snapshot, ref shapes, masks);
            using var stamps = new NativeArray<int>(33 * 33, Allocator.Temp);
            using var weldVertices = new NativeArray<PackedBrickVertex>(33 * 33, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            using var weldIndices = new NativeArray<uint>(33 * 33, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int stamp = 0;
            for (int direction = 0; direction < 6; direction++)
            {
                int axis = direction / 2, sign = (direction & 1) == 0 ? 1 : -1;
                for (int slice = 0; slice < 16; slice++)
                {
                    stamp++;
                    int rowCount = axis == 0 ? 256 : 16;
                    for (int r = 0; r < rowCount; r++)
                    {
                        int y = axis == 0 ? r & 15 : axis == 1 ? slice : r;
                        int z = axis == 0 ? r >> 4 : axis == 1 ? r : slice;
                        int row = (y + 1) + 18 * (z + 1);
                        uint candidates = masks[(8 + direction) * Rows + row];
                        uint hidden;
                        if (axis == 0)
                        {
                            uint neighbour = masks[(2 + (direction ^ 1)) * Rows + row];
                            hidden = sign > 0 ? neighbour >> 1 : neighbour << 1;
                            candidates &= 1u << (slice + 1);
                        }
                        else hidden = masks[(2 + (direction ^ 1)) * Rows + row + sign * (axis == 1 ? 1 : 18)];
                        uint exposed = candidates & ~hidden & Interior;
                        while (exposed != 0)
                        {
                            int x = math.tzcnt(exposed) - 1; exposed &= exposed - 1;
                            int3 cell = new int3(x,y,z); uint value = snapshot[MeshCell.SnapIndex(x,y,z)];
                            int table = MeshCell.Shape(value) * 24 + MeshCell.Orient(value);
                            for (int f = 0; f < shapes.FaceCount[table]; f++)
                            {
                                var face = shapes.Faces[shapes.FirstFace[table] + f];
                                if (face.Plane != direction) continue;
                                Emit(face, cell, value, masks, stamps, weldVertices, weldIndices, stamp, ref output);
                                break;
                            }
                        }
                    }
                }
            }
            for (int z = 0; z < 16; z++) for (int y = 0; y < 16; y++)
            {
                uint structural = masks[Rows + y + 1 + 18 * (z + 1)] & Interior;
                while (structural != 0)
                {
                    int x = math.tzcnt(structural) - 1; structural &= structural - 1;
                    uint value = snapshot[MeshCell.SnapIndex(x,y,z)]; int table = MeshCell.Shape(value) * 24 + MeshCell.Orient(value);
                    for (int f = 0; f < shapes.FaceCount[table]; f++)
                    {
                        var face = shapes.Faces[shapes.FirstFace[table] + f];
                        if (face.Plane == 255) Emit(face, new int3(x,y,z), value, masks, stamps, weldVertices, weldIndices, 0, ref output);
                    }
                }
            }
            min = vertices.Length == 0 ? float3.zero : output.Min; max = vertices.Length == 0 ? float3.zero : output.Max;
        }
        static void BuildMasks(NativeArray<uint> snapshot, ref BrickShapeTables shapes, NativeArray<uint> masks)
        {
            for (int z = -1; z <= 16; z++) for (int y = -1; y <= 16; y++) for (int x = -1; x <= 16; x++)
            {
                uint cell = snapshot[MeshCell.SnapIndex(x,y,z)]; int cls = MeshCell.Class(cell); if (cls == 0) continue;
                int row = y + 1 + 18 * (z + 1); uint bit = 1u << (x + 1); masks[row] |= bit;
                int table = MeshCell.Shape(cell) * 24 + MeshCell.Orient(cell);
                int full = cls == 1 ? shapes.FullFaces[table] : 63;
                for (int f = 0; f < 6; f++) if ((full & (1 << f)) != 0) masks[(2 + f) * Rows + row] |= bit;
                if (cls != 1) continue;
                masks[Rows + row] |= bit;
                for (int f = 0; f < shapes.FaceCount[table]; f++)
                {
                    int plane = shapes.Faces[shapes.FirstFace[table] + f].Plane;
                    if (plane != 255) masks[(8 + plane) * Rows + row] |= bit;
                }
            }
        }
        static bool Solid(NativeArray<uint> masks, int3 p) => (masks[p.y + 1 + 18 * (p.z + 1)] & (1u << (p.x + 1))) != 0;
        static int CornerAO(NativeArray<uint> masks, int3 cell, int direction, int su, int sv)
        {
            int axis = direction / 2, u = (axis + 1) % 3, v = (axis + 2) % 3;
            int3 outside = cell + BrickMath.FaceDirection((BlockFace)direction), side1 = outside, side2 = outside, corner = outside;
            side1[u] += su; side2[v] += sv; corner[u] += su; corner[v] += sv;
            bool a = Solid(masks,side1), b = Solid(masks,side2);
            return a && b ? 0 : 3 - (a ? 1 : 0) - (b ? 1 : 0) - (Solid(masks,corner) ? 1 : 0);
        }
        static int AO(NativeArray<uint> masks, int3 cell, int direction, int3 vertex)
        {
            int axis = direction / 2, u = vertex[(axis + 1) % 3], v = vertex[(axis + 2) % 3];
            int result = 3;
            for (int su = u == 2 ? 1 : -1; su <= (u == 0 ? -1 : 1); su += 2)
                for (int sv = v == 2 ? 1 : -1; sv <= (v == 0 ? -1 : 1); sv += 2)
                    result = math.min(result, CornerAO(masks,cell,direction,su,sv));
            return result;
        }
        static int3 Vertex(ShapeFace face, int i) => i == 0 ? face.V0 : i == 1 ? face.V1 : i == 2 ? face.V2 : face.V3;
        static void Emit(ShapeFace face, int3 cell, uint value, NativeArray<uint> masks, NativeArray<int> stamps,
            NativeArray<PackedBrickVertex> weldVertices, NativeArray<uint> weldIndices, int stamp, ref Output output)
        {
            uint4 ids = default; int4 ao = default;
            for (int k = 0; k < face.VertexCount; k++)
            {
                int3 local = Vertex(face,k), position = cell * 2 + local;
                ao[k] = face.Plane == 255 ? 3 : AO(masks,cell,face.Plane,local);
                var packed = PackedBrickVertex.Pack(position.x,position.y,position.z,face.Normal,MeshCell.Layer(value),MeshCell.Colour(value),
                    ao[k],MeshCell.Damage(value),MeshCell.Scorched(value));
                if (face.Plane == 255) { ids[k] = output.Add(packed); continue; }
                int axis = face.Plane / 2, corner = position[(axis + 1) % 3] + 33 * position[(axis + 2) % 3];
                if (stamps[corner] == stamp && weldVertices[corner].Lo == packed.Lo && weldVertices[corner].Hi == packed.Hi) ids[k] = weldIndices[corner];
                else
                {
                    ids[k] = output.Add(packed); stamps[corner] = stamp; weldVertices[corner] = packed; weldIndices[corner] = ids[k];
                }
            }
            if (face.VertexCount == 3) { Triangle(ids.x,ids.y,ids.z,ref output); return; }
            if (ao.x + ao.z < ao.y + ao.w) { Triangle(ids.x,ids.y,ids.w,ref output); Triangle(ids.y,ids.z,ids.w,ref output); }
            else { Triangle(ids.x,ids.y,ids.z,ref output); Triangle(ids.x,ids.z,ids.w,ref output); }
        }
        static void Triangle(uint a, uint b, uint c, ref Output output)
        { output.Indices.Add(a); output.Indices.Add(b); output.Indices.Add(c); }
        public static void MeshLod1(NativeArray<uint> snapshot, ref BrickShapeTables shapes, NativeList<PackedBrickVertex> vertices,
            NativeList<uint> indices, out float3 min, out float3 max) => throw new System.NotImplementedException("REN-007");
    }
}
