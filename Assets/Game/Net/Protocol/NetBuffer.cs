using System;
using System.IO;
using System.IO.Compression;
using Unity.Mathematics;

namespace Game.Net.Protocol
{
    /// <summary>Growable little-endian byte writer for message payloads (SPEC-90 §3.5). Main thread; reuse with <see cref="Clear"/>.</summary>
    public sealed class NetWriter
    {
        byte[] data;
        public int Length { get; private set; }
        public byte[] Data => data;
        public NetWriter(int capacity = 256) { data = new byte[Math.Max(16, capacity)]; }
        public void Clear() => Length = 0;
        void Ensure(int extra)
        {
            if (Length + extra <= data.Length) return;
            int size = data.Length * 2; while (size < Length + extra) size *= 2;
            Array.Resize(ref data, size);
        }
        public void U8(byte v) { Ensure(1); data[Length++] = v; }
        public void Bool(bool v) => U8(v ? (byte)1 : (byte)0);
        public void U16(ushort v) { Ensure(2); data[Length++] = (byte)v; data[Length++] = (byte)(v >> 8); }
        public void I16(short v) => U16(unchecked((ushort)v));
        public void U32(uint v) { Ensure(4); for (int i = 0; i < 4; i++) data[Length++] = (byte)(v >> (8 * i)); }
        public void I32(int v) => U32(unchecked((uint)v));
        public void U64(ulong v) { Ensure(8); for (int i = 0; i < 8; i++) data[Length++] = (byte)(v >> (8 * i)); }
        public unsafe void F32(float v) => U32(*(uint*)&v);
        public unsafe void F64(double v) => U64(*(ulong*)&v);
        public void F2(float2 v) { F32(v.x); F32(v.y); }
        public void F3(float3 v) { F32(v.x); F32(v.y); F32(v.z); }
        public void D3(double3 v) { F64(v.x); F64(v.y); F64(v.z); }
        public void I3(int3 v) { I32(v.x); I32(v.y); I32(v.z); }
        public void Quat(quaternion q) { F32(q.value.x); F32(q.value.y); F32(q.value.z); F32(q.value.w); }
        public void Bytes(byte[] source, int offset, int count) { Ensure(count); Buffer.BlockCopy(source, offset, data, Length, count); Length += count; }
        public void Str(string s)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? "");
            U16((ushort)Math.Min(bytes.Length, ushort.MaxValue)); Bytes(bytes, 0, Math.Min(bytes.Length, ushort.MaxValue));
        }
        /// <summary>Reserves a u32 written later by <see cref="PatchU32"/>.</summary>
        public int ReserveU32() { int at = Length; U32(0); return at; }
        public void PatchU32(int at, uint v) { for (int i = 0; i < 4; i++) data[at + i] = (byte)(v >> (8 * i)); }
        /// <summary>Appends <paramref name="body"/> deflated, prefixed by the raw and packed lengths.</summary>
        public void Deflated(NetWriter body)
        {
            using var packed = new MemoryStream();
            using (var deflate = new DeflateStream(packed, CompressionLevel.Fastest, true)) deflate.Write(body.data, 0, body.Length);
            U32((uint)body.Length); U32((uint)packed.Length);
            Bytes(packed.GetBuffer(), 0, (int)packed.Length);
        }
    }

    /// <summary>Bounds-checked reader over one payload. Throws <see cref="NetFormatException"/> on truncated data.</summary>
    public sealed class NetReader
    {
        byte[] data; int end;
        public int Position { get; private set; }
        public int Remaining => end - Position;
        public NetReader() { }
        public NetReader(byte[] data, int offset, int count) => Reset(data, offset, count);
        public void Reset(byte[] source, int offset, int count) { data = source; Position = offset; end = offset + count; }
        void Need(int n) { if (Position + n > end) throw new NetFormatException("truncated message"); }
        public byte U8() { Need(1); return data[Position++]; }
        public bool Bool() => U8() != 0;
        public ushort U16() { Need(2); ushort v = (ushort)(data[Position] | (data[Position + 1] << 8)); Position += 2; return v; }
        public short I16() => unchecked((short)U16());
        public uint U32() { Need(4); uint v = 0; for (int i = 0; i < 4; i++) v |= (uint)data[Position + i] << (8 * i); Position += 4; return v; }
        public int I32() => unchecked((int)U32());
        public ulong U64() { Need(8); ulong v = 0; for (int i = 0; i < 8; i++) v |= (ulong)data[Position + i] << (8 * i); Position += 8; return v; }
        public unsafe float F32() { uint v = U32(); return *(float*)&v; }
        public unsafe double F64() { ulong v = U64(); return *(double*)&v; }
        public float2 F2() => new float2(F32(), F32());
        public float3 F3() => new float3(F32(), F32(), F32());
        public double3 D3() => new double3(F64(), F64(), F64());
        public int3 I3() => new int3(I32(), I32(), I32());
        public quaternion Quat() => new quaternion(F32(), F32(), F32(), F32());
        public string Str() { int n = U16(); Need(n); var s = System.Text.Encoding.UTF8.GetString(data, Position, n); Position += n; return s; }
        public void Skip(int n) { Need(n); Position += n; }
        /// <summary>Reads a block written by <see cref="NetWriter.Deflated"/> into a fresh reader.</summary>
        public NetReader Inflated()
        {
            int raw = (int)U32(), packed = (int)U32(); Need(packed);
            if (raw < 0 || raw > 64 * 1024 * 1024) throw new NetFormatException("inflated block too large");
            var output = new byte[raw];
            using (var input = new MemoryStream(data, Position, packed))
            using (var inflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < raw) { int n = inflate.Read(output, read, raw - read); if (n <= 0) break; read += n; }
                if (read != raw) throw new NetFormatException("inflated block shorter than declared");
            }
            Position += packed;
            return new NetReader(output, 0, raw);
        }
    }

    public sealed class NetFormatException : Exception { public NetFormatException(string message) : base(message) { } }
}
