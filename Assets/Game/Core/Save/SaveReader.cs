namespace Game.Core.Save
{
    /// <summary>Binary reader for save payloads; malformed input throws SaveFormatException (SPEC-02 §4.7, §5.7).</summary>
    public sealed class SaveReader
    {
        public const int MaxStringBytes = 16 * 1024 * 1024;
        public static SaveReader Empty => throw new System.NotImplementedException("CORE-007");                      // zero-length payload; IsAtEnd == true
        public SaveReader(byte[] data) { throw new System.NotImplementedException("CORE-007"); }
        public SaveReader(byte[] data, int offset, int count) { throw new System.NotImplementedException("CORE-007"); }
        public SaveReader(System.ReadOnlyMemory<byte> payload, ISaveIdPaletteReader palette = null, ISaveJsonCodec json = null) { throw new System.NotImplementedException("CORE-007"); }
        public int Position => throw new System.NotImplementedException("CORE-007");                                // relative to offset
        public int Length => throw new System.NotImplementedException("CORE-007");
        public int Remaining => throw new System.NotImplementedException("CORE-007");
        public bool IsAtEnd => throw new System.NotImplementedException("CORE-007");
        public byte ReadByte() { throw new System.NotImplementedException("CORE-007"); }
        public sbyte ReadSByte() { throw new System.NotImplementedException("CORE-007"); }
        public bool ReadBool() { throw new System.NotImplementedException("CORE-007"); }
        public ushort ReadUInt16() { throw new System.NotImplementedException("CORE-007"); }
        public short ReadInt16() { throw new System.NotImplementedException("CORE-007"); }
        public uint ReadUInt32() { throw new System.NotImplementedException("CORE-007"); }
        public int ReadInt32() { throw new System.NotImplementedException("CORE-007"); }
        public ulong ReadUInt64() { throw new System.NotImplementedException("CORE-007"); }
        public long ReadInt64() { throw new System.NotImplementedException("CORE-007"); }
        public float ReadSingle() { throw new System.NotImplementedException("CORE-007"); }
        public double ReadDouble() { throw new System.NotImplementedException("CORE-007"); }
        public uint ReadVarUInt32() { throw new System.NotImplementedException("CORE-007"); }
        public ulong ReadVarUInt64() { throw new System.NotImplementedException("CORE-007"); }
        public int ReadVarInt32() { throw new System.NotImplementedException("CORE-007"); }
        public long ReadVarInt64() { throw new System.NotImplementedException("CORE-007"); }
        public string ReadString() { throw new System.NotImplementedException("CORE-007"); }
        public Game.Core.ContentId ReadContentId() { throw new System.NotImplementedException("CORE-007"); }                 // palette != null: palette.Get(ReadUInt16()); else new ContentId(ReadString()); no validation
        public T ReadJson<T>() { throw new System.NotImplementedException("CORE-007"); }                                     // InvalidOperationException without a codec; codec exceptions → SaveFormatException
        public Unity.Mathematics.float2 ReadFloat2() { throw new System.NotImplementedException("CORE-007"); }
        public Unity.Mathematics.float3 ReadFloat3() { throw new System.NotImplementedException("CORE-007"); }
        public Unity.Mathematics.float4 ReadFloat4() { throw new System.NotImplementedException("CORE-007"); }
        public Unity.Mathematics.quaternion ReadQuaternion() { throw new System.NotImplementedException("CORE-007"); }
        public Unity.Mathematics.double3 ReadDouble3() { throw new System.NotImplementedException("CORE-007"); }
        public Unity.Mathematics.int3 ReadInt3() { throw new System.NotImplementedException("CORE-007"); }
        public void ReadBytes(System.Span<byte> destination) { throw new System.NotImplementedException("CORE-007"); }
        public byte[] ReadByteArray() { throw new System.NotImplementedException("CORE-007"); }
        public Unity.Collections.NativeArray<T> ReadNativeArray<T>(Unity.Collections.Allocator allocator) where T : unmanaged { throw new System.NotImplementedException("CORE-007"); }
        public SaveBlock BeginBlock() { throw new System.NotImplementedException("CORE-007"); }                              // reads UInt32 length, validates <= Remaining
        public void EndBlock(SaveBlock block) { throw new System.NotImplementedException("CORE-007"); }                      // overread → throw; otherwise skips to block end
        public void Skip(int count) { throw new System.NotImplementedException("CORE-007"); }
    }
}
