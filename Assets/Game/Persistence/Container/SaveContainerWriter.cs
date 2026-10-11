using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Game.Persistence.Container
{
    /// <summary>Writes the world.sav container, format 1 (SPEC-05 §4.2, §5.5). Implements SPEC-05 §4.5.</summary>
    public static class SaveContainerWriter
    {
        internal const int TableEntrySize = 20;
        internal const int MaxChunks = 4096;
        internal const int MaxRawLength = 268_435_456;
        internal static readonly byte[] Magic = { 0x53, 0x53, 0x47, 0x53 };   // "SSGS"

        /// <summary>Compresses and checksums each chunk, writes header + table + payloads. Thread-agnostic. Always writes the newest
        /// format; header.FormatVersion, HeaderFlags, ChunkCount and HeaderCrc32 are ignored (computed). A payload is deflated only when
        /// it is at least <paramref name="minCompressBytes"/> long and the deflated form is shorter. Same input → same bytes.</summary>
        public static byte[] Write(in ContainerHeader header, IReadOnlyList<RawChunk> chunks, int minCompressBytes)
        {
            Validate(chunks);
            int count = chunks.Count;
            var stored = new byte[count][];
            var flags = new ChunkFlags[count];
            long total = ContainerHeader.Size + (long)TableEntrySize * count;
            for (int i = 0; i < count; i++)
            {
                var raw = chunks[i].Payload;
                stored[i] = raw;
                if (raw.Length >= minCompressBytes && raw.Length > 0)
                {
                    var deflated = Deflate(raw);
                    if (deflated.Length < raw.Length) { stored[i] = deflated; flags[i] = ChunkFlags.Deflate; }
                }
                total += stored[i].Length;
            }
            if (total > int.MaxValue) throw new ArgumentException("SaveContainerWriter: the container would exceed 2 GB");

            var file = new byte[total];
            var span = file.AsSpan();
            Magic.CopyTo(span);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(4), ContainerHeader.CurrentFormatVersion);
            BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(6), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8), header.DatabaseHash32);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(12), header.SavedUtcTicks);
            BinaryPrimitives.WriteInt64LittleEndian(span.Slice(20), BitConverter.DoubleToInt64Bits(header.PlaytimeSeconds));
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(28), (uint)count);

            int at = ContainerHeader.Size;
            for (int i = 0; i < count; i++, at += TableEntrySize)
            {
                var e = span.Slice(at, TableEntrySize);
                var key = chunks[i].Key;
                for (int k = 0; k < 4; k++) e[k] = (byte)key[k];
                BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(4), chunks[i].Version);
                BinaryPrimitives.WriteUInt16LittleEndian(e.Slice(6), (ushort)flags[i]);
                BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(8), (uint)stored[i].Length);
                BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(12), (uint)chunks[i].Payload.Length);
                BinaryPrimitives.WriteUInt32LittleEndian(e.Slice(16), Crc32.Compute(stored[i]));
            }
            uint headerCrc = Crc32.Append(Crc32.Compute(span.Slice(0, 32)), span.Slice(ContainerHeader.Size, TableEntrySize * count));
            BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(32), headerCrc);

            for (int i = 0; i < count; i++) { stored[i].CopyTo(span.Slice(at)); at += stored[i].Length; }
            return file;
        }

        static void Validate(IReadOnlyList<RawChunk> chunks)
        {
            if (chunks == null) throw new ArgumentNullException(nameof(chunks));
            if (chunks.Count < 2 || chunks.Count > MaxChunks) throw new ArgumentException($"SaveContainerWriter: {chunks.Count} chunks (allowed 2..{MaxChunks}: META, IDPL and participants)");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in chunks)
            {
                if (c == null) throw new ArgumentException("SaveContainerWriter: null chunk");
                if (!IsValidKey(c.Key)) throw new ArgumentException($"SaveContainerWriter: invalid chunk key '{c.Key}' (4 chars [A-Z0-9])");
                if (!seen.Add(c.Key)) throw new ArgumentException($"SaveContainerWriter: duplicate chunk key '{c.Key}'");
                if (c.Version == 0) throw new ArgumentException($"SaveContainerWriter: chunk '{c.Key}' has version 0 (1..65535)");
                if (c.Payload == null) throw new ArgumentException($"SaveContainerWriter: chunk '{c.Key}' has no payload");
                if (c.Payload.Length > MaxRawLength) throw new ArgumentException($"SaveContainerWriter: chunk '{c.Key}' is {c.Payload.Length} bytes (max {MaxRawLength})");
            }
        }

        internal static bool IsValidKey(string key)
        {
            if (key == null || key.Length != 4) return false;
            foreach (var ch in key) if (!IsKeyChar((byte)ch) || ch > 0x7F) return false;
            return true;
        }

        internal static bool IsKeyChar(byte b) => (b >= (byte)'A' && b <= (byte)'Z') || (b >= (byte)'0' && b <= (byte)'9');

        static byte[] Deflate(byte[] raw)
        {
            using var ms = new MemoryStream(raw.Length / 2 + 64);
            using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true)) ds.Write(raw, 0, raw.Length);
            return ms.ToArray();
        }
    }
}
