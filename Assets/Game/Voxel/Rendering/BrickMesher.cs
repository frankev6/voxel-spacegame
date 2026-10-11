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
        /// <summary>SPEC-11 §5.3: majority downsampling and exact greedy rectangles of matching attributes.</summary>
        public static void MeshLod1(NativeArray<uint> snapshot, ref BrickShapeTables shapes, NativeList<PackedBrickVertex> vertices,
            NativeList<uint> indices, out float3 min, out float3 max)
        {
            vertices.Clear(); indices.Clear();
            var output = new Output { Vertices = vertices, Indices = indices, Min = new float3(float.PositiveInfinity), Max = new float3(float.NegativeInfinity) };
            using var super = new NativeArray<uint>(512, Allocator.Temp);
            using var faceKeys = new NativeArray<uint>(64, Allocator.Temp);
            Downsample(snapshot, super);
            for (int direction = 0; direction < 6; direction++) for (int slice = 0; slice < 8; slice++)
            {
                BuildFaceKeys(snapshot, super, direction, slice, faceKeys);
                Greedy(faceKeys, direction, slice, ref output);
            }
            min = vertices.Length == 0 ? float3.zero : output.Min; max = vertices.Length == 0 ? float3.zero : output.Max;
        }
        static int SuperIndex(int3 p) => p.x + 8 * p.y + 64 * p.z;
        static void Downsample(NativeArray<uint> snapshot, NativeArray<uint> super)
        {
            for (int z = 0; z < 8; z++) for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                int solids = 0; var keys = new FixedList64Bytes<uint>();
                for (int c = 0; c < 8; c++)
                {
                    uint child = snapshot[MeshCell.SnapIndex(2*x+(c&1), 2*y+((c>>1)&1), 2*z+(c>>2))];
                    int cls = MeshCell.Class(child); if (cls != 0) solids++;
                    if (cls == 1) keys.Add((uint)(MeshCell.Layer(child) * 256 + MeshCell.Colour(child)));
                }
                if (solids < 4) continue;
                uint best = uint.MaxValue; int bestCount = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    int count = 0; for (int j = 0; j < keys.Length; j++) if (keys[j] == keys[i]) count++;
                    if (count > bestCount || count == bestCount && keys[i] < best) { best = keys[i]; bestCount = count; }
                }
                // Solid occupancy is independent of whether any structural child supplies a visible attribute.
                super[x + 8*y + 64*z] = 0x80000000u | (bestCount == 0 ? 0 : best + 1);
            }
        }
        static void BuildFaceKeys(NativeArray<uint> snapshot, NativeArray<uint> super, int direction, int slice, NativeArray<uint> keys)
        {
            int axis = direction / 2, u = (axis + 1) % 3, v = (axis + 2) % 3, sign = (direction & 1) == 0 ? 1 : -1;
            for (int row = 0; row < 8; row++) for (int col = 0; col < 8; col++)
            {
                int3 p = default; p[axis] = slice; p[u] = col; p[v] = row;
                uint key = super[SuperIndex(p)] & 0x7FFFFFFFu;
                if (key != 0)
                {
                    int3 neighbour = p; neighbour[axis] += sign; bool hidden;
                    if (neighbour[axis] >= 0 && neighbour[axis] < 8) hidden = (super[SuperIndex(neighbour)] & 0x80000000u) != 0;
                    else
                    {
                        int solids = 0; int3 border = p * 2; border[axis] = sign < 0 ? -1 : 16;
                        for (int a = 0; a < 2; a++) for (int b = 0; b < 2; b++)
                        {
                            int3 sample = border; sample[u] += a; sample[v] += b;
                            if (MeshCell.Class(snapshot[MeshCell.SnapIndex(sample.x,sample.y,sample.z)]) != 0) solids++;
                        }
                        hidden = solids >= 2;
                    }
                    if (hidden) key = 0;
                }
                keys[col + 8*row] = key;
            }
        }
        static void Greedy(NativeArray<uint> keys, int direction, int slice, ref Output output)
        {
            int axis = direction / 2, u = (axis + 1) % 3, v = (axis + 2) % 3;
            for (int row = 0; row < 8; row++) for (int col = 0; col < 8; col++)
            {
                uint key = keys[col + 8*row]; if (key == 0) continue;
                int width = 1; while (col + width < 8 && keys[col + width + 8*row] == key) width++;
                int height = 1;
                while (row + height < 8)
                {
                    bool same = true;
                    for (int x = 0; x < width; x++) if (keys[col+x + 8*(row+height)] != key) { same = false; break; }
                    if (!same) break; height++;
                }
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) keys[col+x + 8*(row+y)] = 0;
                uint attribute = key - 1; uint4 ids = default;
                for (int corner = 0; corner < 4; corner++)
                {
                    int3 p = default; p[axis] = slice + ((direction & 1) == 0 ? 1 : 0);
                    p[u] = col + (corner == 1 || corner == 2 ? width : 0); p[v] = row + (corner >= 2 ? height : 0); p *= 4;
                    ids[corner] = output.Add(PackedBrickVertex.Pack(p.x,p.y,p.z,direction,(int)(attribute >> 8),(int)(attribute & 255),3,0,false));
                }
                if ((direction & 1) == 0) { Triangle(ids.x,ids.y,ids.z,ref output); Triangle(ids.x,ids.z,ids.w,ref output); }
                else { Triangle(ids.x,ids.w,ids.z,ref output); Triangle(ids.x,ids.z,ids.y,ref output); }
            }
        }
    }
}
