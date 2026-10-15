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
                float3 position = vertex.Position;
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
            MeshXeno(snapshot, 1, ref output);
            if (indices.Length == 0) vertices.Clear();   // the pool treats any vertex without an index as invalid geometry
            min = vertices.Length == 0 ? float3.zero : output.Min; max = vertices.Length == 0 ? float3.zero : output.Max;
        }
        static void BuildMasks(NativeArray<uint> snapshot, ref BrickShapeTables shapes, NativeArray<uint> masks)
        {
            for (int z = -1; z <= 16; z++) for (int y = -1; y <= 16; y++) for (int x = -1; x <= 16; x++)
            {
                uint cell = snapshot[MeshCell.SnapIndex(x,y,z)]; int cls = MeshCell.Class(cell); if (cls == 0) continue;
                int row = y + 1 + 18 * (z + 1); uint bit = 1u << (x + 1); masks[row] |= bit;
                // ADR-0115: Xeno tissue hides neighbouring faces like a cube but emits none; the surface-nets pass shells it.
                if (MeshCell.IsXeno(cell)) { for (int f = 0; f < 6; f++) masks[(2 + f) * Rows + row] |= bit; continue; }
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
            MeshXeno(snapshot, 2, ref output);
            if (indices.Length == 0) vertices.Clear();
            min = vertices.Length == 0 ? float3.zero : output.Min; max = vertices.Length == 0 ? float3.zero : output.Max;
        }
        static int SuperIndex(int3 p) => p.x + 8 * p.y + 64 * p.z;

        // ---------------------------------------------------------------------------------------------------------------
        // ADR-0115 Xeno surface nets. Samples sit at cell centres: air +0.5, Xeno tissue -(depth + 1/16), other solids -0.5
        // (functional cells inside a Xeno brick count as tissue so organs never leave holes). A vertex sits in every dual
        // cube with a sign change, at the mean of its edge crossings; a quad is emitted for each sign-changing edge whose
        // inside end is tissue, owned by the brick holding the edge's lower sample. Stride 2 meshes LOD1 from 2x2x2 means.
        // QA-001 (owner: "i couldn't break their bodies though its like theyre invincible"): the depth is set at spawn, so a cell
        // exposed by damage still carried its old depth and the shell regrew over the hole (a crater's floor rose 0.4 cells).
        // A tissue cell touching air sits at most ~1 cell below the grown surface (the field is a distance); one deeper than 1.2
        // cells that touches air was exposed by damage, a fresh wound: its geometry uses a shallow depth so the hole opens, and
        // `shade` keeps the true depth so the wound still glows as flesh. Sacs (the maximum depth code) keep their bulge.
        const float WoundDepth = .2f;
        static bool Wound(NativeArray<uint> snapshot, int3 c, uint v)
        {
            if (((v >> 5) & 31) >= 31 || MeshCell.XenoDepthCells(v) <= 1.2f) return false;
            for (int f = 0; f < 6; f++)
            {
                int3 n = c; n[f >> 1] += (f & 1) == 0 ? 1 : -1; n = math.clamp(n, -1, 16);
                if (MeshCell.Class(snapshot[MeshCell.SnapIndex(n.x, n.y, n.z)]) == 0) return true;
            }
            return false;
        }
        static float XenoSample(NativeArray<uint> snapshot, int3 s, int stride, bool organs, out uint value, out bool tissue, out float shade)
        {
            float sum = 0, raw = 0; int solid = 0, count = 0; value = 0; tissue = false;
            for (int i = 0; i < stride * stride * stride; i++)
            {
                int3 c = s * stride + new int3(i % stride, (i / stride) % stride, i / (stride * stride));
                c = math.clamp(c, -1, 16); count++;
                uint v = snapshot[MeshCell.SnapIndex(c.x, c.y, c.z)]; int cls = MeshCell.Class(v);
                if (cls == 0) { sum += .5f; raw += .5f; continue; }
                bool xeno = MeshCell.IsXeno(v) || organs && cls == 2;
                float depth = xeno ? (MeshCell.IsXeno(v) ? MeshCell.XenoDepthCells(v) : .5f) + 1f / 16 : .5f;
                raw -= depth; sum -= MeshCell.IsXeno(v) && Wound(snapshot, c, v) ? WoundDepth + 1f / 16 : depth; solid++;
                if (xeno && (!tissue || MeshCell.IsXeno(v) && !MeshCell.IsXeno(value))) { value = v; tissue = true; }
            }
            shade = stride == 1 ? -raw : -raw / count;
            return stride == 1 ? sum : solid * 2 >= count ? math.min(sum / count, -1f / 16) : math.max(sum / count, 1f / 16);
        }
        static void MeshXeno(NativeArray<uint> snapshot, int stride, ref Output output)
        {
            bool any = false;
            for (int i = 0; i < MeshCell.SnapCells && !any; i++) any = MeshCell.IsXeno(snapshot[i]);
            if (!any) return;
            int n = 16 / stride;                       // samples -1..n (n + 2 per axis)
            int side = n + 2;
            int cubes = side - 1;                      // cube min corners -1..n-1
            var field = new NativeArray<float>(side * side * side, Allocator.Temp);
            var values = new NativeArray<uint>(side * side * side, Allocator.Temp);
            var tissue = new NativeArray<bool>(side * side * side, Allocator.Temp);
            var shade = new NativeArray<float>(side * side * side, Allocator.Temp);
            var vertexOf = new NativeArray<int>(cubes * cubes * cubes, Allocator.Temp);
            var pending = new NativeArray<PackedBrickVertex>(cubes * cubes * cubes, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            try
            {
                for (int z = -1; z <= n; z++) for (int y = -1; y <= n; y++) for (int x = -1; x <= n; x++)
                {
                    int k = (x + 1) + side * (y + 1) + side * side * (z + 1);
                    field[k] = XenoSample(snapshot, new int3(x, y, z), stride, true, out uint v, out bool t, out float d);
                    values[k] = v; tissue[k] = t; shade[k] = d;
                }
                MeshXenoQuads(field, shade, values, tissue, vertexOf, pending, side, cubes, n, stride, ref output);
            }
            finally { field.Dispose(); shade.Dispose(); values.Dispose(); tissue.Dispose(); vertexOf.Dispose(); pending.Dispose(); }
        }
        static void MeshXenoQuads(NativeArray<float> field, NativeArray<float> shade, NativeArray<uint> values, NativeArray<bool> tissue, NativeArray<int> vertexOf,
            NativeArray<PackedBrickVertex> pending, int side, int cubes, int n, int stride, ref Output output)
        {
            for (int i = 0; i < vertexOf.Length; i++) vertexOf[i] = -1;
            float half = stride * .5f;
            for (int z = -1; z < n; z++) for (int y = -1; y < n; y++) for (int x = -1; x < n; x++)
            {
                float3 crossing = 0; int crossings = 0; uint attribute = 0; bool haveAttribute = false; float shallowest = float.MaxValue;
                FixedList64Bytes<float> corner = default;
                for (int c = 0; c < 8; c++)
                {
                    int3 s = new int3(x + (c & 1), y + ((c >> 1) & 1), z + (c >> 2));
                    int k = (s.x + 1) + side * (s.y + 1) + side * side * (s.z + 1);
                    corner.Add(field[k]);
                    if (field[k] < 0 && tissue[k])
                    {
                        if (!haveAttribute) { attribute = values[k]; haveAttribute = true; }
                        // The shallowest tissue corner says how deep this surface cuts: ~0 on the grown shell, > 1 in a wound.
                        shallowest = math.min(shallowest, shade[k]);
                    }
                }
                if (!haveAttribute) continue;
                for (int e = 0; e < 12; e++)
                {
                    int axis = e / 4, a = e % 4;
                    int3 p0 = axis == 0 ? new int3(0, a & 1, a >> 1) : axis == 1 ? new int3(a & 1, 0, a >> 1) : new int3(a & 1, a >> 1, 0);
                    int3 p1 = p0; p1[axis] = 1;
                    float v0 = corner[p0.x + 2 * p0.y + 4 * p0.z], v1 = corner[p1.x + 2 * p1.y + 4 * p1.z];
                    if (v0 < 0 == v1 < 0) continue;
                    float t = v0 / (v0 - v1);
                    crossing += math.lerp((float3)p0, (float3)p1, t); crossings++;
                }
                if (crossings == 0) continue;
                float3 local = crossing / crossings;
                // Trilinear gradient of the field at the vertex: points from tissue (negative) to air (positive), i.e. outward.
                float3 l = local, il = 1 - local;
                float3 g = new float3(
                    (corner[1] - corner[0]) * il.y * il.z + (corner[3] - corner[2]) * l.y * il.z + (corner[5] - corner[4]) * il.y * l.z + (corner[7] - corner[6]) * l.y * l.z,
                    (corner[2] - corner[0]) * il.x * il.z + (corner[3] - corner[1]) * l.x * il.z + (corner[6] - corner[4]) * il.x * l.z + (corner[7] - corner[5]) * l.x * l.z,
                    (corner[4] - corner[0]) * il.x * il.y + (corner[5] - corner[1]) * l.x * il.y + (corner[6] - corner[2]) * il.x * l.y + (corner[7] - corner[3]) * l.x * l.y);
                float3 normal = math.lengthsq(g) > 1e-10f ? math.normalize(g) : new float3(0, 1, 0);
                // Sample s sits at the centre of its (stride-sized) cell: brick position = (s + local) * stride + stride/2.
                float3 position = (new float3(x, y, z) + local) * stride + half;
                int depth = (int)math.round(math.max(0, shallowest - 1f / 16) * 4);
                // Emitted lazily by the first quad that uses it: cubes whose quads all belong to a neighbour brick add nothing.
                int slot = (x + 1) + cubes * (y + 1) + cubes * cubes * (z + 1);
                pending[slot] = PackedBrickVertex.PackSmooth(position, normal, MeshCell.Layer(attribute), MeshCell.Colour(attribute), depth, MeshCell.Damage(attribute));
                vertexOf[slot] = -2;
            }
            for (int z = 0; z < n; z++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int k0 = (x + 1) + side * (y + 1) + side * side * (z + 1);
                for (int axis = 0; axis < 3; axis++)
                {
                    int3 q = new int3(x, y, z); q[axis]++;
                    int k1 = (q.x + 1) + side * (q.y + 1) + side * side * (q.z + 1);
                    float v0 = field[k0], v1 = field[k1];
                    if (v0 < 0 == v1 < 0) continue;
                    bool insideFirst = v0 < 0;
                    if (!(insideFirst ? tissue[k0] : tissue[k1])) continue;
                    int u = (axis + 1) % 3, v = (axis + 2) % 3;
                    int4 ids = default; bool ok = true;
                    for (int c = 0; c < 4; c++)
                    {
                        int3 cube = new int3(x, y, z); cube[u] -= (c == 1 || c == 2) ? 1 : 0; cube[v] -= c >= 2 ? 1 : 0;
                        if (math.any(cube < -1) || math.any(cube >= n)) { ok = false; break; }
                        int slot = (cube.x + 1) + cubes * (cube.y + 1) + cubes * cubes * (cube.z + 1), id = vertexOf[slot];
                        if (id == -1) { ok = false; break; }
                        ids[c] = slot;
                    }
                    if (!ok) continue;
                    for (int c = 0; c < 4; c++)
                    {
                        int slot = ids[c];
                        if (vertexOf[slot] == -2) vertexOf[slot] = (int)output.Add(pending[slot]);
                        ids[c] = vertexOf[slot];
                    }
                    // ids walk (0,0) -> (-u) -> (-u,-v) -> (-v) around the edge; flip so the quad faces the outside.
                    if (insideFirst) { Triangle((uint)ids.x, (uint)ids.y, (uint)ids.z, ref output); Triangle((uint)ids.x, (uint)ids.z, (uint)ids.w, ref output); }
                    else { Triangle((uint)ids.x, (uint)ids.z, (uint)ids.y, ref output); Triangle((uint)ids.x, (uint)ids.w, (uint)ids.z, ref output); }
                }
            }
        }
        static void Downsample(NativeArray<uint> snapshot, NativeArray<uint> super)
        {
            for (int z = 0; z < 8; z++) for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            {
                int solids = 0; var keys = new FixedList64Bytes<uint>();
                for (int c = 0; c < 8; c++)
                {
                    uint child = snapshot[MeshCell.SnapIndex(2*x+(c&1), 2*y+((c>>1)&1), 2*z+(c>>2))];
                    int cls = MeshCell.Class(child); if (cls != 0) solids++;
                    if (cls == 1 && !MeshCell.IsXeno(child)) keys.Add((uint)(MeshCell.Layer(child) * 256 + MeshCell.Colour(child)));
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
