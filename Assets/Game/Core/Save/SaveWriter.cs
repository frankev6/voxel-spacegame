using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Game.Core.Save
{
    /// <summary>Deterministic little-endian binary writer for save payloads (SPEC-02 §4.7, §5.7). Single thread per instance.</summary>
    public sealed class SaveWriter : IDisposable
    {
        public const int DefaultCapacity = 4096;
        [StaticStateOk("immutable, stateless encoding")] static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, false);   // lone surrogates → U+FFFD (frozen behaviour)

        readonly ISaveIdPalette palette;
        readonly ISaveJsonCodec json;
        byte[] buffer;
        int length;
        readonly List<int> openBlocks = new List<int>(4);                       // positions of the 4-byte length placeholders
        readonly List<(int Offset, ISaveDeferredBlock Block)> deferred = new List<(int, ISaveDeferredBlock)>();
        bool disposed;

        /// <summary>palette: null → ContentIds are written as strings; json: null → WriteJson throws InvalidOperationException.
        /// The buffer is rented from ArrayPool&lt;byte&gt;.Shared.</summary>
        public SaveWriter(ISaveIdPalette palette = null, ISaveJsonCodec json = null, int initialCapacity = DefaultCapacity)
        {
            this.palette = palette;
            this.json = json;
            buffer = ArrayPool<byte>.Shared.Rent(Math.Max(16, initialCapacity));
        }

        public int Length { get { Check(); return length; } }                  // bytes written so far, excluding deferred blocks
        public int DeferredCount { get { Check(); return deferred.Count; } }

        public void Reset() { Check(); length = 0; openBlocks.Clear(); deferred.Clear(); }   // open blocks and deferred blocks discarded

        /// <summary>For an abandoned save (SPEC-05 §5.4 steps 3 and 16): disposes every deferred block not yet encoded and forgets them.
        /// Each Dispose runs in its own try/catch so one failure cannot leak the rest. Works with open blocks and after Dispose of the
        /// writer. SPEC-02 open question 14.</summary>
        public void DisposeDeferredBlocks()
        {
            foreach (var d in deferred)
            {
                try { d.Block?.Dispose(); }
                catch (Exception e) { Game.Core.GameLog.Exception("Save", e, "disposing a deferred save block"); }
            }
            deferred.Clear();
        }

        public void WriteByte(byte value) { Span(1)[0] = value; }
        public void WriteSByte(sbyte value) { Span(1)[0] = (byte)value; }
        public void WriteBool(bool value) { Span(1)[0] = value ? (byte)1 : (byte)0; }
        public void WriteUInt16(ushort value) { BinaryPrimitives.WriteUInt16LittleEndian(Span(2), value); }
        public void WriteInt16(short value) { BinaryPrimitives.WriteInt16LittleEndian(Span(2), value); }
        public void WriteUInt32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(Span(4), value); }
        public void WriteInt32(int value) { BinaryPrimitives.WriteInt32LittleEndian(Span(4), value); }
        public void WriteUInt64(ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(Span(8), value); }
        public void WriteInt64(long value) { BinaryPrimitives.WriteInt64LittleEndian(Span(8), value); }
        public void WriteSingle(float value) { WriteInt32(BitConverter.SingleToInt32Bits(value)); }     // IEEE-754 bits, LE; NaN payload and −0 kept
        public void WriteDouble(double value) { WriteInt64(BitConverter.DoubleToInt64Bits(value)); }

        public void WriteVarUInt32(uint value) { WriteVarUInt64(value); }                                // LEB128
        public void WriteVarUInt64(ulong value)
        {
            if (value < 0x80) { Span(1)[0] = (byte)value; return; }
            int size = 1;
            for (ulong v = value >> 7; v != 0; v >>= 7) size++;
            var s = Span(size);
            for (int i = 0; i < size - 1; i++) { s[i] = (byte)(value | 0x80); value >>= 7; }
            s[size - 1] = (byte)value;
        }
        public void WriteVarInt32(int value) { WriteVarUInt32((uint)((value << 1) ^ (value >> 31))); }     // zig-zag, then LEB128
        public void WriteVarInt64(long value) { WriteVarUInt64((ulong)((value << 1) ^ (value >> 63))); }

        /// <summary>null → VarUInt32(0); else VarUInt32(byteCount + 1) + UTF-8 bytes.</summary>
        public void WriteString(string value)
        {
            Check();
            if (value == null) { WriteByte(0); return; }
            int n = value.Length < 64 && IsAscii(value) ? value.Length : Utf8.GetByteCount(value);
            WriteVarUInt32((uint)n + 1);
            Utf8.GetBytes(value.AsSpan(), Span(n));
        }

        /// <summary>palette != null: WriteUInt16(palette.GetOrAdd(id)); else WriteString(id.Value).</summary>
        public void WriteContentId(in ContentId id)
        {
            Check();
            if (palette != null) WriteUInt16(palette.GetOrAdd(id));
            else WriteString(id.Value);
        }

        /// <summary>VarUInt32(length) + UTF-8 JSON from the codec.</summary>
        public void WriteJson<T>(T value)
        {
            Check();
            if (json == null) throw new InvalidOperationException("SaveWriter has no JSON codec");
            var bytes = json.Serialize(value);
            WriteByteArray(bytes);
        }

        /// <summary>Placeholder; becomes VarUInt32(length) + encoded bytes in ToArray(encoder). Forbidden inside an open block.</summary>
        public void WriteDeferred(ISaveDeferredBlock block)
        {
            Check();
            if (block == null) throw new ArgumentNullException(nameof(block));
            if (openBlocks.Count > 0) throw new InvalidOperationException("WriteDeferred inside an open block would make the block length wrong");
            deferred.Add((length, block));
        }

        public void WriteFloat2(float2 v) { WriteSingle(v.x); WriteSingle(v.y); }
        public void WriteFloat3(float3 v) { WriteSingle(v.x); WriteSingle(v.y); WriteSingle(v.z); }
        public void WriteFloat4(float4 v) { WriteSingle(v.x); WriteSingle(v.y); WriteSingle(v.z); WriteSingle(v.w); }
        public void WriteQuaternion(quaternion q) { WriteFloat4(q.value); }                              // x, y, z, w
        public void WriteDouble3(double3 v) { WriteDouble(v.x); WriteDouble(v.y); WriteDouble(v.z); }
        public void WriteInt3(int3 v) { WriteInt32(v.x); WriteInt32(v.y); WriteInt32(v.z); }

        public void WriteBytes(ReadOnlySpan<byte> bytes) { bytes.CopyTo(Span(bytes.Length)); }          // raw
        public void WriteByteArray(ReadOnlySpan<byte> bytes) { WriteVarUInt32((uint)bytes.Length); WriteBytes(bytes); }   // VarUInt32 length + raw

        /// <summary>VarUInt32 count + raw element bytes.</summary>
        public unsafe void WriteNativeArray<T>(NativeArray<T> array) where T : unmanaged
        {
            Check();
            WriteVarUInt32((uint)array.Length);
            int bytes = array.Length * sizeof(T);
            if (bytes == 0) return;
            var dst = Span(bytes);
            fixed (byte* d = dst) UnsafeUtility.MemCpy(d, NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array), bytes);
        }

        /// <summary>Writes a 4-byte placeholder; EndBlock back-patches the length.</summary>
        public SaveBlock BeginBlock()
        {
            Check();
            int at = length;
            Span(4);
            openBlocks.Add(at);
            return new SaveBlock(at, openBlocks.Count);
        }

        /// <summary>Back-patches the UInt32 byte length; LIFO only (InvalidOperationException otherwise).</summary>
        public void EndBlock(SaveBlock block)
        {
            Check();
            if (openBlocks.Count == 0 || block.Depth != openBlocks.Count || openBlocks[openBlocks.Count - 1] != block.Start)
                throw new InvalidOperationException("EndBlock does not match the innermost open BeginBlock");
            BinaryPrimitives.WriteUInt32LittleEndian(new Span<byte>(buffer, block.Start, 4), (uint)(length - block.Start - 4));
            openBlocks.RemoveAt(openBlocks.Count - 1);
        }

        /// <summary>Valid until the next write or Dispose; InvalidOperationException with open blocks or deferred blocks.</summary>
        public ReadOnlySpan<byte> AsSpan() { CheckComplete(); return new ReadOnlySpan<byte>(buffer, 0, length); }

        public byte[] ToArray()
        {
            CheckComplete();
            var result = new byte[length];
            Buffer.BlockCopy(buffer, 0, result, 0, length);
            return result;
        }

        /// <summary>Splices VarUInt32(len) + encodeDeferred(block) at each deferred position, in order. Thread-agnostic; never calls Encode or
        /// Dispose itself.</summary>
        public byte[] ToArray(Func<ISaveDeferredBlock, byte[]> encodeDeferred)
        {
            Check();
            if (openBlocks.Count > 0) throw new InvalidOperationException("ToArray with an open block");
            if (deferred.Count == 0) return ToArray();
            if (encodeDeferred == null) throw new ArgumentNullException(nameof(encodeDeferred));
            var encoded = new byte[deferred.Count][];
            int total = length;
            for (int i = 0; i < deferred.Count; i++)
            {
                encoded[i] = encodeDeferred(deferred[i].Block) ?? throw new InvalidOperationException("A deferred block encoded to null");
                total += VarSize((uint)encoded[i].Length) + encoded[i].Length;
            }
            var result = new byte[total];
            int src = 0, dst = 0;
            for (int i = 0; i < deferred.Count; i++)
            {
                int upTo = deferred[i].Offset;
                Buffer.BlockCopy(buffer, src, result, dst, upTo - src); dst += upTo - src; src = upTo;
                uint n = (uint)encoded[i].Length;
                while (n >= 0x80) { result[dst++] = (byte)(n | 0x80); n >>= 7; }
                result[dst++] = (byte)n;
                Buffer.BlockCopy(encoded[i], 0, result, dst, encoded[i].Length); dst += encoded[i].Length;
            }
            Buffer.BlockCopy(buffer, src, result, dst, length - src);
            return result;
        }

        /// <summary>Returns the buffer; does NOT dispose deferred blocks (their owner does); later calls throw ObjectDisposedException.</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ArrayPool<byte>.Shared.Return(buffer);
            buffer = null;
        }

        static bool IsAscii(string s) { foreach (char c in s) if (c >= 0x80) return false; return true; }

        static int VarSize(uint n) { int s = 1; while (n >= 0x80) { n >>= 7; s++; } return s; }

        /// <summary>Reserves n bytes at the end and returns them; grows by renting max(2 × capacity, needed) from the pool.</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        Span<byte> Span(int n)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SaveWriter));
            int needed = length + n;
            if (needed > buffer.Length)
            {
                var next = ArrayPool<byte>.Shared.Rent(Math.Max(2 * buffer.Length, needed));
                Buffer.BlockCopy(buffer, 0, next, 0, length);
                ArrayPool<byte>.Shared.Return(buffer);
                buffer = next;
            }
            var span = new Span<byte>(buffer, length, n);
            length = needed;
            return span;
        }

        void Check() { if (disposed) throw new ObjectDisposedException(nameof(SaveWriter)); }

        void CheckComplete()
        {
            Check();
            if (openBlocks.Count > 0) throw new InvalidOperationException("The writer has an open block");
            if (deferred.Count > 0) throw new InvalidOperationException("The writer has deferred blocks: use ToArray(encoder)");
        }
    }
}
