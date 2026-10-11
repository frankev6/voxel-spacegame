namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5. A chunk as stored: fourCC, version, flags and the RAW (decompressed) payload.</summary>
    public sealed class RawChunk
    {
        public string Key; public ushort Version; public ChunkFlags StoredFlags;
        public byte[] Payload;          // decompressed
        public bool Corrupt;            // CRC or decompression failed; Payload is null
    }
}
