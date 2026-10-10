namespace Game.Core.Save
{
    /// <summary>Deterministic little-endian binary writer for save payloads (SPEC-02 §4.7, §5.7).</summary>
    public sealed class SaveWriter : System.IDisposable
    {
        public const int DefaultCapacity = 4096;
        /// <summary>palette: null → ContentIds are written as strings; json: null → WriteJson throws InvalidOperationException.
        /// The buffer is rented from ArrayPool&lt;byte&gt;.Shared.</summary>
        public SaveWriter(ISaveIdPalette palette = null, ISaveJsonCodec json = null, int initialCapacity = DefaultCapacity) { throw new System.NotImplementedException("CORE-007"); }
        public int Length => throw new System.NotImplementedException("CORE-007");                                   // bytes written so far, excluding deferred blocks
        public int DeferredCount => throw new System.NotImplementedException("CORE-007");
        public void Reset() { throw new System.NotImplementedException("CORE-007"); }                                        // Length = 0; open blocks discarded
        public void WriteByte(byte value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteSByte(sbyte value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteBool(bool value) { throw new System.NotImplementedException("CORE-007"); }                          // 0x00 / 0x01
        public void WriteUInt16(ushort value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteInt16(short value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteUInt32(uint value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteInt32(int value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteUInt64(ulong value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteInt64(long value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteSingle(float value) { throw new System.NotImplementedException("CORE-007"); }                       // IEEE-754 bits, LE
        public void WriteDouble(double value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteVarUInt32(uint value) { throw new System.NotImplementedException("CORE-007"); }                     // LEB128
        public void WriteVarUInt64(ulong value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteVarInt32(int value) { throw new System.NotImplementedException("CORE-007"); }                       // zig-zag, then LEB128
        public void WriteVarInt64(long value) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteString(string value) { throw new System.NotImplementedException("CORE-007"); }                      // null → VarUInt32(0); else VarUInt32(byteCount + 1) + UTF-8 bytes
        public void WriteContentId(in Game.Core.ContentId id) { throw new System.NotImplementedException("CORE-007"); }      // palette != null: WriteUInt16(palette.GetOrAdd(id)); else WriteString(id.Value)
        public void WriteJson<T>(T value) { throw new System.NotImplementedException("CORE-007"); }                          // VarUInt32(length) + UTF-8 JSON from the codec
        public void WriteDeferred(ISaveDeferredBlock block) { throw new System.NotImplementedException("CORE-007"); }        // placeholder; becomes VarUInt32(length) + Encode() bytes in ToArray(encoder)
        public void WriteFloat2(Unity.Mathematics.float2 v) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteFloat3(Unity.Mathematics.float3 v) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteFloat4(Unity.Mathematics.float4 v) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteQuaternion(Unity.Mathematics.quaternion q) { throw new System.NotImplementedException("CORE-007"); } // x, y, z, w
        public void WriteDouble3(Unity.Mathematics.double3 v) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteInt3(Unity.Mathematics.int3 v) { throw new System.NotImplementedException("CORE-007"); }
        public void WriteBytes(System.ReadOnlySpan<byte> bytes) { throw new System.NotImplementedException("CORE-007"); }    // raw
        public void WriteByteArray(System.ReadOnlySpan<byte> bytes) { throw new System.NotImplementedException("CORE-007"); }// VarUInt32 length + raw
        public void WriteNativeArray<T>(Unity.Collections.NativeArray<T> array) where T : unmanaged { throw new System.NotImplementedException("CORE-007"); }   // VarUInt32 count + raw element bytes
        public SaveBlock BeginBlock() { throw new System.NotImplementedException("CORE-007"); }                              // writes a 4-byte placeholder
        public void EndBlock(SaveBlock block) { throw new System.NotImplementedException("CORE-007"); }                      // back-patches UInt32 byte length; LIFO only
        public System.ReadOnlySpan<byte> AsSpan() { throw new System.NotImplementedException("CORE-007"); }                  // valid until the next write or Dispose; InvalidOperationException if DeferredCount > 0
        public byte[] ToArray() { throw new System.NotImplementedException("CORE-007"); }                                    // InvalidOperationException if DeferredCount > 0
        public byte[] ToArray(System.Func<ISaveDeferredBlock, byte[]> encodeDeferred) { throw new System.NotImplementedException("CORE-007"); }   // splices each deferred block in place
        public void Dispose() { throw new System.NotImplementedException("CORE-007"); }                                      // returns the buffer; does NOT dispose deferred blocks (their owner does); later calls throw ObjectDisposedException
    }
}
