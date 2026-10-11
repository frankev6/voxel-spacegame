using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Core.Save
{
    /// <summary>Binary reader for save payloads; malformed input throws SaveFormatException only (SPEC-02 §4.7, §5.7). Single thread per instance.</summary>
    public sealed class SaveReader
    {
        public const int MaxStringBytes = 16 * 1024 * 1024;
        [StaticStateOk("immutable, stateless encoding")] static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

        [StaticStateOk("immutable: zero-length payload, every read throws and Position stays 0")]
        static readonly SaveReader EmptyReader = new SaveReader(Array.Empty<byte>());

        readonly ReadOnlyMemory<byte> data;
        readonly ISaveIdPaletteReader palette;
        readonly ISaveJsonCodec json;
        readonly List<int> blockEnds = new List<int>(4);
        int pos;

        public static SaveReader Empty => EmptyReader;                         // zero-length payload; IsAtEnd == true
        public SaveReader(byte[] data) : this(new ReadOnlyMemory<byte>(data ?? throw new ArgumentNullException(nameof(data)))) { }
        public SaveReader(byte[] data, int offset, int count) : this(new ReadOnlyMemory<byte>(data, offset, count)) { }
        public SaveReader(ReadOnlyMemory<byte> payload, ISaveIdPaletteReader palette = null, ISaveJsonCodec json = null)
        { data = payload; this.palette = palette; this.json = json; }

        public int Position => pos;                                             // relative to offset
        public int Length => data.Length;
        /// <summary>Bytes left before the innermost open block's end (or the payload end).</summary>
        public int Remaining => Limit - pos;
        public bool IsAtEnd => pos >= data.Length;
        int Limit => blockEnds.Count > 0 ? blockEnds[blockEnds.Count - 1] : data.Length;

        ReadOnlySpan<byte> Take(int n)
        {
            if (n < 0 || n > Limit - pos) throw new SaveFormatException("unexpected end", pos);
            var s = data.Span.Slice(pos, n);
            pos += n;
            return s;
        }

        public byte ReadByte() => Take(1)[0];
        public sbyte ReadSByte() => (sbyte)Take(1)[0];
        public bool ReadBool()
        {
            byte b = Take(1)[0];
            if (b > 1) throw new SaveFormatException("invalid bool " + b, pos - 1);
            return b == 1;
        }
        public ushort ReadUInt16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
        public short ReadInt16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
        public uint ReadUInt32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
        public int ReadInt32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
        public ulong ReadUInt64() => BinaryPrimitives.ReadUInt64LittleEndian(Take(8));
        public long ReadInt64() => BinaryPrimitives.ReadInt64LittleEndian(Take(8));
        public float ReadSingle() => BitConverter.Int32BitsToSingle(ReadInt32());
        public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadInt64());

        /// <summary>At most 5 bytes; a 5th byte above 0x0F is an overflow.</summary>
        public uint ReadVarUInt32()
        {
            int start = pos;
            uint result = 0;
            for (int i = 0; i < 5; i++)
            {
                byte b = ReadByte();
                if (i == 4 && b > 0x0F) throw new SaveFormatException("varint overflow", start);
                result |= (uint)(b & 0x7F) << (7 * i);
                if ((b & 0x80) == 0) return result;
            }
            throw new SaveFormatException("varint overflow", start);
        }

        /// <summary>At most 10 bytes; a 10th byte above 0x01 is an overflow.</summary>
        public ulong ReadVarUInt64()
        {
            int start = pos;
            ulong result = 0;
            for (int i = 0; i < 10; i++)
            {
                byte b = ReadByte();
                if (i == 9 && b > 0x01) throw new SaveFormatException("varint overflow", start);
                result |= (ulong)(b & 0x7F) << (7 * i);
                if ((b & 0x80) == 0) return result;
            }
            throw new SaveFormatException("varint overflow", start);
        }

        public int ReadVarInt32() { uint v = ReadVarUInt32(); return (int)(v >> 1) ^ -(int)(v & 1); }
        public long ReadVarInt64() { ulong v = ReadVarUInt64(); return (long)(v >> 1) ^ -(long)(v & 1); }

        public string ReadString()
        {
            uint prefix = ReadVarUInt32();
            if (prefix == 0) return null;
            uint n = prefix - 1;
            if (n > MaxStringBytes || n > (uint)Remaining) throw new SaveFormatException("string length " + n + " exceeds the data", pos);
            var bytes = Take((int)n);
            try { return StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException) { throw new SaveFormatException("invalid UTF-8", pos - (int)n); }
        }

        /// <summary>palette != null: palette.Get(ReadUInt16()); else new ContentId(ReadString()); no validation.</summary>
        public ContentId ReadContentId() => palette != null ? palette.Get(ReadUInt16()) : new ContentId(ReadString());

        /// <summary>InvalidOperationException without a codec; codec exceptions → SaveFormatException.</summary>
        public T ReadJson<T>()
        {
            if (json == null) throw new InvalidOperationException("SaveReader has no JSON codec");
            uint n = ReadVarUInt32();
            if (n > (uint)Remaining) throw new SaveFormatException("JSON length " + n + " exceeds the data", pos);
            int at = pos;
            var slice = Take((int)n);
            try { return json.Deserialize<T>(slice); }
            catch (Exception e) { throw new SaveFormatException("invalid JSON: " + e.Message, at); }
        }

        public float2 ReadFloat2() => new float2(ReadSingle(), ReadSingle());
        public float3 ReadFloat3() => new float3(ReadSingle(), ReadSingle(), ReadSingle());
        public float4 ReadFloat4() => new float4(ReadSingle(), ReadSingle(), ReadSingle(), ReadSingle());
        public quaternion ReadQuaternion() => new quaternion(ReadFloat4());
        public double3 ReadDouble3() => new double3(ReadDouble(), ReadDouble(), ReadDouble());
        public int3 ReadInt3() => new int3(ReadInt32(), ReadInt32(), ReadInt32());

        public void ReadBytes(Span<byte> destination) { Take(destination.Length).CopyTo(destination); }

        /// <summary>Reads VarUInt32 n then n bytes: both WriteByteArray values and spliced deferred blocks.</summary>
        public byte[] ReadByteArray()
        {
            uint n = ReadVarUInt32();
            if (n > (uint)Remaining) throw new SaveFormatException("byte array length " + n + " exceeds the data", pos);
            return Take((int)n).ToArray();
        }

        /// <summary>Validates count × sizeof(T) against the remaining bytes before allocating.</summary>
        public unsafe NativeArray<T> ReadNativeArray<T>(Allocator allocator) where T : unmanaged
        {
            uint count = ReadVarUInt32();
            long bytes = (long)count * sizeof(T);
            if (bytes > Remaining) throw new SaveFormatException("native array of " + count + " elements exceeds the data", pos);
            var array = new NativeArray<T>((int)count, allocator, NativeArrayOptions.UninitializedMemory);
            if (bytes > 0)
            {
                var src = Take((int)bytes);
                fixed (byte* s = src) UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafePtr(array), s, bytes);
            }
            return array;
        }

        /// <summary>Reads the UInt32 length and validates it against Remaining.</summary>
        public SaveBlock BeginBlock()
        {
            uint len = ReadUInt32();
            if (len > (uint)Remaining) throw new SaveFormatException("block length " + len + " exceeds the data", pos - 4);
            int end = pos + (int)len;
            blockEnds.Add(end);
            return new SaveBlock(end, blockEnds.Count);
        }

        /// <summary>Skips any unread tail (forward compatibility). Overread → SaveFormatException; a mismatched block → InvalidOperationException.</summary>
        public void EndBlock(SaveBlock block)
        {
            if (blockEnds.Count == 0 || block.Depth != blockEnds.Count || blockEnds[blockEnds.Count - 1] != block.Start)
                throw new InvalidOperationException("EndBlock does not match the innermost open BeginBlock");
            if (pos > block.Start) throw new SaveFormatException("read past the end of a block", pos);
            pos = block.Start;
            blockEnds.RemoveAt(blockEnds.Count - 1);
        }

        public void Skip(int count) { Take(count); }
    }
}
