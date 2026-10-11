using System.Collections.Generic;

namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5.</summary>
    public static class SaveContainerWriter
    {
        /// <summary>Compresses and checksums each chunk, writes header + table + payloads. Thread-agnostic.</summary>
        public static byte[] Write(in ContainerHeader header, IReadOnlyList<RawChunk> chunks, int minCompressBytes) { throw new System.NotImplementedException("PERS-003"); }
    }
}
