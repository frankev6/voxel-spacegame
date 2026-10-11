using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Game.Core;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Voxel.Data
{
    /// <summary>SPEC-05 §4.4/§5.7: deterministic, self-contained voxel serialization.</summary>
    public static unsafe class VoxsCodec
    {
        public const ushort Version = 1;
        static readonly ProfilerMarker EncodeMarker = new ProfilerMarker(ProfilerCategory.Scripts, "VoxsCodec.Encode");
        static readonly ProfilerMarker DecodeMarker = new ProfilerMarker(ProfilerCategory.Scripts, "VoxsCodec.Decode");
        const int MaxBodyBytes = 268435456;
        const int MaxDecodedBricks = MaxBodyBytes / (4096 * 4);
        [StaticStateOk("immutable UTF-8 encoding")] static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public static int CellIndex(int3 local) => local.x + 16 * (local.y + 16 * local.z);
        static int Compare(int3 a, int3 b)
        { int c = a.z.CompareTo(b.z); if (c == 0) c = a.y.CompareTo(b.y); return c == 0 ? a.x.CompareTo(b.x) : c; }
        static void Coord(SaveWriter w, int3 c) { w.WriteVarInt32(c.x); w.WriteVarInt32(c.y); w.WriteVarInt32(c.z); }
        static int3 Coord(SaveReader r) => new int3(r.ReadVarInt32(), r.ReadVarInt32(), r.ReadVarInt32());
        static void Require(bool valid, string reason, int offset = 0)
        { if (!valid) throw new SaveFormatException(reason, offset); }
        static int Count(SaveReader r, int max, int minBytes = 0)
        {
            uint n = r.ReadVarUInt32();
            Require(n <= max && (long)n * minBytes <= r.Remaining, "count exceeds limit or remaining bytes", r.Position);
            return (int)n;
        }
        static string Name(SaveReader r)
        {
            int n = Count(r, 256, 1);
            var bytes = new byte[n]; r.ReadBytes(bytes);
            try { return Utf8.GetString(bytes); }
            catch (DecoderFallbackException) { throw new SaveFormatException("invalid UTF-8", r.Position - n); }
        }
        static void Name(SaveWriter w, string name)
        {
            byte[] bytes = Utf8.GetBytes(name);
            if (bytes.Length > 256) throw new ArgumentException("VOXS block name exceeds 256 bytes");
            w.WriteByteArray(bytes);
        }
        static int[] Sorted(NativeArray<int3> coords)
        {
            var order = new int[coords.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) => Compare(coords[a], coords[b]));
            for (int i = 1; i < order.Length; i++)
                if (Compare(coords[order[i - 1]], coords[order[i]]) == 0) throw new ArgumentException("Duplicate VOXS brick coordinate");
            return order;
        }
        public static byte[] Encode(in VoxsSource source, VoxsBlockNames names, VoxsEncodeOptions options)
        {
            using var marker = EncodeMarker.Auto();
            if (names == null) throw new ArgumentNullException(nameof(names));
            if (source.Palette.Length > 32768 || source.Instances.Length > 32768 || source.BrickCoords.Length > MaxDecodedBricks ||
                (long)source.BrickCoords.Length * 4096 != source.Cells.Length || source.AnchorInstance < -1 || source.AnchorInstance >= source.Instances.Length ||
                !math.isfinite(source.CellSize) || source.CellSize <= 0 || (!source.Partial && source.RemovedBricks.Length != 0))
                throw new ArgumentException("Invalid VOXS source dimensions, counts or anchor");
            var table = new Dictionary<ushort, uint>();
            var blocks = new List<ushort>();
            void Add(ushort block, byte orient)
            {
                if (orient >= 24) throw new ArgumentException("Invalid VOXS orientation");
                if (block == 0 || table.ContainsKey(block)) return;
                if (names.Get(block) == null) throw new ArgumentException("Unknown VOXS block index " + block);
                blocks.Add(block); table.Add(block, (uint)blocks.Count);
            }
            for (int i = 0; i < source.Palette.Length; i++) Add(source.Palette[i].Block, source.Palette[i].Orient);
            for (int i = 0; i < source.Instances.Length; i++) Add(source.Instances[i].Block, source.Instances[i].Orient);
            int[] sorted = Sorted(source.BrickCoords), removed = Sorted(source.RemovedBricks);
            int kept = 0;
            var solidCounts = new int[sorted.Length];
            Cell* cells = (Cell*)source.Cells.GetUnsafeReadOnlyPtr();
            for (int b = 0; b < sorted.Length; b++)
            {
                int solid = 0;
                for (int j = 0; j < 4096; j++)
                {
                    Cell c = cells[b * 4096 + j];
                    if (c.Ref == 0)
                    { if (c.Hp != 0 || c.Flags != 0) throw new ArgumentException("VOXS air must be all zero"); }
                    else
                    {
                        if (c.Hp == 0 || (c.IsInstance ? c.InstanceIndex >= source.Instances.Length : c.Ref >= source.Palette.Length))
                            throw new ArgumentException("Invalid VOXS cell ref or HP");
                        solid++;
                    }
                }
                solidCounts[b] = solid; if (solid != 0) kept++;
            }
            using var w = new SaveWriter(initialCapacity: 64 + sorted.Length * 64);
            w.WriteSingle(source.CellSize); w.WriteInt32(source.AnchorInstance);
            w.WriteVarUInt32((uint)blocks.Count);
            foreach (ushort block in blocks) Name(w, names.Get(block));
            w.WriteVarUInt32((uint)source.Palette.Length);
            for (int i = 0; i < source.Palette.Length; i++)
            { var p = source.Palette[i]; w.WriteVarUInt32(p.Block == 0 ? 0 : table[p.Block]); w.WriteByte(p.Orient); w.WriteByte(p.Color); }
            w.WriteVarUInt32((uint)source.Instances.Length);
            for (int i = 0; i < source.Instances.Length; i++)
            {
                var p = source.Instances[i]; w.WriteVarUInt32(p.Block == 0 ? 0 : table[p.Block]); w.WriteByte(p.Orient); w.WriteByte(p.Color);
                Coord(w, p.Origin); w.WriteSingle(p.FunctionHp);
            }
            w.WriteVarUInt32((uint)kept);
            // Managed save workers have no Unity Temp memory scope. This short-lived
            // buffer is disposed before Encode returns, including on failure.
            var runs = new UnsafeList<byte>(16384, Allocator.TempJob);
            try
            {
                foreach (int b in sorted)
                {
                    if (solidCounts[b] == 0) continue;
                    Cell* brick = cells + b * 4096;
                    Coord(w, source.BrickCoords[b]);
                    bool uniform = true, fullHp = true, zeroFlags = true;
                    for (int j = 0; j < 4096; j++)
                    {
                        Cell c = brick[j]; uniform &= c.Ref == brick[0].Ref;
                        if (c.Ref != 0) { fullHp &= c.Hp == 255; zeroFlags &= c.Flags == 0; }
                    }
                    int order = 3;
                    if (!uniform)
                    {
                        order = 0; int best = VoxsRuns.MeasureRuns(brick, 0);
                        for (int k = 1; k <= 2; k++) { int n = VoxsRuns.MeasureRuns(brick, k); if (n < best) { best = n; order = k; } }
                    }
                    w.WriteByte((byte)order);
                    if (uniform) w.WriteUInt16(brick[0].Ref);
                    else WriteRuns(w, brick, order, 0, &runs);
                    w.WriteByte(fullHp ? (byte)0 : (byte)1);
                    if (!fullHp) WriteRuns(w, brick, order, 1, &runs);
                    w.WriteByte(zeroFlags ? (byte)0 : (byte)1);
                    if (!zeroFlags) WriteRuns(w, brick, order, 2, &runs);
                }
            }
            finally { runs.Dispose(); }
            w.WriteVarUInt32((uint)removed.Length);
            foreach (int b in removed) Coord(w, source.RemovedBricks[b]);
            using var output = new SaveWriter();
            output.WriteUInt32(0x53584f56); output.WriteUInt16(Version);
            output.WriteUInt16((ushort)((options.Deflate ? 1 : 0) | (source.Partial ? 2 : 0)));
            if (options.Deflate)
            {
                using var stream = new MemoryStream();
                using (var deflate = new DeflateStream(stream, CompressionLevel.Optimal, true)) deflate.Write(w.AsSpan());
                output.WriteBytes(stream.ToArray());
            }
            else output.WriteBytes(w.AsSpan());
            return output.ToArray();
        }
        static void WriteRuns(SaveWriter w, Cell* cells, int order, int field, UnsafeList<byte>* runs)
        { runs->Clear(); VoxsRuns.EncodeRuns(cells, order, field, runs); w.WriteBytes(new ReadOnlySpan<byte>(runs->Ptr, runs->Length)); }

        static byte[] Body(ReadOnlySpan<byte> data, out ushort version, out bool partial)
        {
            Require(data.Length >= 8, "truncated header");
            var header = new SaveReader(data.Slice(0, 8).ToArray());
            Require(header.ReadUInt32() == 0x53584f56, "invalid magic");
            version = header.ReadUInt16();
            Require(version == Version, version > Version ? "newer VOXS version" : "invalid VOXS version", 4);
            ushort flags = header.ReadUInt16(); partial = (flags & 2) != 0;
            Require((flags & ~3) == 0, "unknown flags", 6);
            Require(data.Length - 8 <= MaxBodyBytes, "body exceeds byte limit", 8);
            if ((flags & 1) == 0) return data.Slice(8).ToArray();
            using var input = new ExactDeflateInput(data.Slice(8).ToArray());
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            var buffer = new byte[8192]; int count;
            while ((count = deflate.Read(buffer, 0, buffer.Length)) != 0)
            {
                Require(output.Length + count <= MaxBodyBytes, "inflated body exceeds byte limit");
                output.Write(buffer, 0, count);
            }
            Require(!input.ReadPastEnd && input.Position == input.Length, "truncated Deflate or trailing compressed bytes", 0);
            return output.ToArray();
        }
        // DeflateStream normally reads ahead and accepts an unfinished stream. One-byte input
        // exposes its actual end marker and preserves the no-trailing-bytes guarantee.
        sealed class ExactDeflateInput : MemoryStream
        {
            public bool ReadPastEnd;
            public ExactDeflateInput(byte[] bytes) : base(bytes, false) { }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = base.Read(buffer, offset, Math.Min(count, 1));
                if (n == 0 && count > 0) ReadPastEnd = true;
                return n;
            }
            public override int Read(Span<byte> buffer)
            {
                int n = base.Read(buffer.Slice(0, Math.Min(buffer.Length, 1)));
                if (n == 0 && buffer.Length > 0) ReadPastEnd = true;
                return n;
            }
        }
        public static bool TryPeek(ReadOnlySpan<byte> data, out ushort version, out bool partial, out int brickCount)
        {
            version = 0; partial = false; brickCount = 0;
            try
            {
                var r = new SaveReader(Body(data, out version, out partial));
                r.Skip(8); int blocks = Count(r, 65535, 1);
                for (int i = 0; i < blocks; i++) Name(r);
                int palette = Count(r, 32768, 3);
                for (int i = 0; i < palette; i++) { Count(r, blocks); Require(r.ReadByte() < 24, "orientation"); r.Skip(1); }
                int instances = Count(r, 32768, 10);
                for (int i = 0; i < instances; i++) { Count(r, blocks); Require(r.ReadByte() < 24, "orientation"); r.Skip(1); Coord(r); r.Skip(4); }
                brickCount = Count(r, 1048576, 8); return true;
            }
            catch (Exception e) when (e is SaveFormatException || e is InvalidDataException || e is IOException) { return false; }
        }
        public static bool TryDecode(ReadOnlySpan<byte> data, ContentCatalog catalog, Allocator allocator, out VoxsDecoded result, out string error)
        {
            using var marker = DecodeMarker.Auto();
            result = null; error = null;
            VoxsDecoded d = null; SaveReader r = null;
            try
            {
                byte[] body = Body(data, out _, out bool partial); r = new SaveReader(body);
                d = new VoxsDecoded { Partial = partial, CellSize = r.ReadSingle(), AnchorInstance = r.ReadInt32() };
                Require(math.isfinite(d.CellSize) && d.CellSize > 0, "invalid cell size", r.Position);
                int blocks = Count(r, 65535, 1);
                var ids = new string[blocks + 1]; var map = new ushort[blocks + 1];
                for (int i = 1; i <= blocks; i++)
                {
                    ids[i] = Name(r);
                    map[i] = catalog.GetBlockIndex(catalog.ResolveBlockOrMissing(new ContentId(ids[i]), "VOXS"));
                }
                int palettes = Count(r, 32768, 3);
                d.Palette = new NativeList<PaletteEntry>(palettes, allocator);
                var paletteNames = new string[palettes];
                for (int i = 0; i < palettes; i++)
                {
                    int block = Count(r, blocks); byte orient = r.ReadByte(), color = r.ReadByte();
                    Require(orient < 24, "invalid orientation", r.Position - 2);
                    paletteNames[i] = ids[block];
                    d.Palette.Add(new PaletteEntry { Block = map[block], Orient = orient, Color = color });
                }
                int instances = Count(r, 32768, 10);
                Require(d.AnchorInstance >= -1 && d.AnchorInstance < instances, "invalid anchor", 4);
                d.Instances = new NativeList<BlockInstance>(instances, allocator);
                var instanceNames = new string[instances];
                for (int i = 0; i < instances; i++)
                {
                    int block = Count(r, blocks); byte orient = r.ReadByte(), color = r.ReadByte();
                    Require(orient < 24, "invalid orientation", r.Position - 2);
                    instanceNames[i] = ids[block];
                    d.Instances.Add(new BlockInstance { Block = map[block], Orient = orient, Color = color, Origin = Coord(r), FunctionHp = r.ReadSingle() });
                }
                int bricks = Count(r, 1048576, 8);
                Require(bricks <= MaxDecodedBricks, "decoded cells exceed 256 MiB limit", r.Position);
                d.BrickCoords = new NativeList<int3>(bricks, allocator);
                d.Cells = new NativeList<Cell>(bricks * 4096, allocator); d.Cells.Resize(bricks * 4096, NativeArrayOptions.ClearMemory);
                var seen = new HashSet<int3>();
                Cell* cells = (Cell*)d.Cells.GetUnsafePtr();
                fixed (byte* bytes = body)
                for (int b = 0; b < bricks; b++)
                {
                    int3 coord = Coord(r); Require(seen.Add(coord), "duplicate brick coordinate", r.Position); d.BrickCoords.Add(coord);
                    Cell* brick = cells + b * 4096;
                    int order = r.ReadByte(); Require(order <= 3, "invalid traversal", r.Position - 1);
                    if (order == 3) { ushort v = r.ReadUInt16(); for (int j = 0; j < 4096; j++) brick[j].Ref = v; }
                    else ReadRuns(r, bytes, brick, order, 0, 4096);
                    int solid = 0;
                    for (int j = 0; j < 4096; j++)
                    {
                        Cell c = brick[j]; if (c.Ref == 0) continue;
                        Require(c.IsInstance ? c.InstanceIndex < instances : c.Ref < palettes, "cell ref out of range", r.Position);
                        brick[j].Hp = 255; solid++;
                    }
                    Require(solid > 0, "empty brick", r.Position);
                    for (int field = 1; field <= 2; field++)
                    {
                        byte mode = r.ReadByte(); Require(mode <= 1, "invalid run mode", r.Position - 1);
                        if (mode != 0) ReadRuns(r, bytes, brick, order, field, solid);
                    }
                    for (int j = 0; j < 4096; j++) Require(brick[j].Ref == 0 || brick[j].Hp > 0, "solid HP is zero", r.Position);
                }
                int removed = Count(r, 1048576, 3);
                Require(partial || removed == 0, "removed bricks without Partial", r.Position);
                d.RemovedBricks = new NativeList<int3>(removed, allocator); seen.Clear();
                for (int i = 0; i < removed; i++) { int3 c = Coord(r); Require(seen.Add(c), "duplicate removed coordinate", r.Position); d.RemovedBricks.Add(c); }
                Require(r.IsAtEnd, "trailing bytes", r.Position);
                SortDecoded(d);
                Repair(d, catalog, paletteNames, instanceNames);
                result = d; return true;
            }
            catch (Exception e) when (e is SaveFormatException || e is InvalidDataException || e is IOException)
            {
                d?.Dispose();
                int offset = e is SaveFormatException f ? f.Position : r?.Position ?? 0;
                error = $"VOXS offset {offset + (r == null ? 0 : 8)}: {e.Message}"; return false;
            }
        }
        static void ReadRuns(SaveReader r, byte* bytes, Cell* brick, int order, int field, int total)
        {
            int read = VoxsRuns.DecodeRuns(bytes + r.Position, r.Remaining, brick, order, field, total, out int offset);
            Require(read >= 0, "invalid or truncated run", r.Position + offset); r.Skip(read);
        }
        static void SortDecoded(VoxsDecoded d)
        {
            int[] order = Sorted(d.BrickCoords.AsArray()); bool sorted = true;
            for (int i = 0; i < order.Length; i++) sorted &= order[i] == i;
            if (!sorted)
            {
                using var copy = new NativeArray<Cell>(d.Cells.AsArray(), Allocator.Temp);
                using var coords = new NativeArray<int3>(d.BrickCoords.AsArray(), Allocator.Temp);
                for (int i = 0; i < order.Length; i++)
                { d.BrickCoords[i] = coords[order[i]]; NativeArray<Cell>.Copy(copy, order[i] * 4096, d.Cells.AsArray(), i * 4096, 4096); }
            }
            d.RemovedBricks.AsArray().Sort(new CoordinateComparer());
        }
        struct CoordinateComparer : IComparer<int3> { public int Compare(int3 a, int3 b) => VoxsCodec.Compare(a, b); }
        static void Drift(VoxsDecoded d, string id)
        {
            d.DriftWarningCount++;
            id ??= "air";
            GameLog.WarnOnce("VOXS-drift-" + id, "Persistence", "VOXS content drift repaired or retained for " + id);
        }
        static void Repair(VoxsDecoded d, ContentCatalog catalog, string[] paletteNames, string[] instanceNames)
        {
            ushort missing = catalog.GetBlockIndex(catalog.MissingBlock);
            for (int i = 0; i < d.Palette.Length; i++)
            {
                var p = d.Palette[i]; var def = catalog.GetBlock(p.Block);
                if (p.Block != 0 && (p.Block == missing || def.RequiresInstance))
                { Drift(d, paletteNames[i]); p.Block = missing; d.Palette[i] = p; }
            }
            int original = d.Instances.Length;
            var counts = new int[original]; var remap = new ushort[original]; int kept = 0;
            Cell* cells = (Cell*)d.Cells.GetUnsafePtr();
            for (int i = 0; i < d.Cells.Length; i++) if (cells[i].IsInstance) counts[cells[i].InstanceIndex]++;
            var palettes = new Dictionary<uint, ushort>();
            uint Key(PaletteEntry p) => p.Block | ((uint)p.Orient << 16) | ((uint)p.Color << 24);
            for (int i = 1; i < d.Palette.Length; i++) { uint key = Key(d.Palette[i]); if (!palettes.ContainsKey(key)) palettes.Add(key, (ushort)i); }
            int fallback = -1;
            for (int i = 0; i < original; i++)
            {
                var instance = d.Instances[i]; var def = catalog.GetBlock(instance.Block);
                if (def == null || instance.Block == missing || !def.RequiresInstance)
                {
                    Drift(d, instanceNames[i]);
                    var p = new PaletteEntry { Block = def == null ? missing : instance.Block, Orient = instance.Orient, Color = instance.Color };
                    if (!palettes.TryGetValue(Key(p), out ushort index))
                    {
                        if (d.Palette.Length == 0) d.Palette.Add(default);
                        if (d.Palette.Length < 32768)
                        { index = (ushort)d.Palette.Length; d.Palette.Add(p); palettes.Add(Key(p), index); }
                        else
                        {
                            if (fallback < 0)
                            {
                                var f = new PaletteEntry { Block = missing };
                                if (palettes.TryGetValue(Key(f), out ushort found)) fallback = found;
                                else
                                {
                                    fallback = 32767;
                                    uint old = Key(d.Palette[fallback]);
                                    if (palettes.TryGetValue(old, out ushort previous) && previous == fallback) palettes.Remove(old);
                                    d.Palette[fallback] = f; palettes[Key(f)] = (ushort)fallback;
                                    Drift(d, catalog.MissingBlock.Id.Value);
                                }
                            }
                            index = (ushort)fallback;
                        }
                    }
                    remap[i] = index;
                }
                else
                {
                    if (counts[i] != def.Cells.Count) Drift(d, instanceNames[i]);
                    instance.CellsAlive = (ushort)math.min(counts[i], ushort.MaxValue);
                    instance.Flags = def.Kind == Game.Data.Blocks.BlockKind.Functional && instance.FunctionHp <= 0 ? BlockInstanceFlags.Destroyed : BlockInstanceFlags.None;
                    d.Instances[kept] = instance; remap[i] = (ushort)(0x8000 | kept++);
                }
            }
            for (int i = 0; i < d.Cells.Length; i++) if (cells[i].IsInstance) cells[i].Ref = remap[cells[i].InstanceIndex];
            if (d.AnchorInstance >= 0) { ushort m = remap[d.AnchorInstance]; d.AnchorInstance = (m & 0x8000) == 0 ? -1 : m & 0x7fff; }
            d.Instances.ResizeUninitialized(kept);
        }
    }
}
