using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Game.Persistence.Container
{
    /// <summary>Reads and classifies a world.sav container (SPEC-05 §5.5). Never throws on malformed input: every problem maps to a status,
    /// and a bad chunk is marked Corrupt with a PERS-E011 report entry. Implements SPEC-05 §4.5.</summary>
    public static class SaveContainerReader
    {
        const int Entry = SaveContainerWriter.TableEntrySize;

        public static ContainerReadStatus Read(ReadOnlySpan<byte> file, out ContainerHeader header,
            List<RawChunk> chunks, Game.Data.Validation.ValidationReport report, string sourceLabel)
        {
            chunks?.Clear();
            var status = ReadHeaderAndTable(file, out header, out var table, requireWholeFile: true);
            if (status != ContainerReadStatus.Ok) return status;

            bool anyCorrupt = false;
            long offset = ContainerHeader.Size + (long)Entry * header.ChunkCount;
            foreach (var e in table)
            {
                var chunk = ReadChunk(file.Slice((int)offset, (int)e.StoredLength), e, report, sourceLabel);
                anyCorrupt |= chunk.Corrupt;
                chunks?.Add(chunk);
                offset += e.StoredLength;
            }
            return anyCorrupt ? ContainerReadStatus.CorruptChunks : ContainerReadStatus.Ok;
        }

        /// <summary>For slot listing (SPEC-05 §5.2): reads one chunk from a PREFIX of a file (the first 64 KB). The header and table are
        /// verified (CRC included) but not the total length; the chunk must lie entirely inside the prefix. Never throws.</summary>
        internal static bool TryReadChunkFromPrefix(ReadOnlySpan<byte> prefix, string key, out byte[] payload)
        {
            payload = null;
            try
            {
                if (ReadHeaderAndTable(prefix, out var header, out var table, requireWholeFile: false) != ContainerReadStatus.Ok) return false;
                long offset = ContainerHeader.Size + (long)Entry * header.ChunkCount;
                foreach (var e in table)
                {
                    if (e.Key == key)
                    {
                        if (offset + e.StoredLength > prefix.Length) return false;
                        var chunk = ReadChunk(prefix.Slice((int)offset, (int)e.StoredLength), e, null, null);
                        payload = chunk.Payload;
                        return !chunk.Corrupt;
                    }
                    offset += e.StoredLength;
                }
            }
            catch (Exception) { }
            return false;
        }

        struct TableEntry { public string Key; public ushort Version; public ushort Flags; public uint StoredLength, RawLength, Crc; }

        static ContainerReadStatus ReadHeaderAndTable(ReadOnlySpan<byte> file, out ContainerHeader header, out TableEntry[] table, bool requireWholeFile)
        {
            header = default;
            table = null;
            // 1–2. Magic and format version.
            if (file.Length < ContainerHeader.Size || !file.Slice(0, 4).SequenceEqual(SaveContainerWriter.Magic)) return ContainerReadStatus.NotAContainer;
            header.FormatVersion = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(4));
            if (header.FormatVersion == 0) return ContainerReadStatus.NotAContainer;
            if (header.FormatVersion > ContainerHeader.CurrentFormatVersion) return ContainerReadStatus.NewerFormat;
            header.HeaderFlags = BinaryPrimitives.ReadUInt16LittleEndian(file.Slice(6));
            header.DatabaseHash32 = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(8));
            header.SavedUtcTicks = BinaryPrimitives.ReadInt64LittleEndian(file.Slice(12));
            header.PlaytimeSeconds = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(file.Slice(20)));
            header.ChunkCount = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(28));
            header.HeaderCrc32 = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(32));

            // 3. Flags, count, table fits.
            uint count = header.ChunkCount;
            if (header.HeaderFlags != 0 || count < 2 || count > SaveContainerWriter.MaxChunks) return ContainerReadStatus.CorruptHeader;
            long tableEnd = ContainerHeader.Size + (long)Entry * count;
            if (tableEnd > file.Length) return ContainerReadStatus.CorruptHeader;

            // 4. Header CRC over bytes 0..31 and the table.
            uint crc = Crc32.Append(Crc32.Compute(file.Slice(0, 32)), file.Slice(ContainerHeader.Size, (int)(tableEnd - ContainerHeader.Size)));
            if (crc != header.HeaderCrc32) return ContainerReadStatus.CorruptHeader;

            // 5. Table entries.
            table = new TableEntry[count];
            var keys = new HashSet<string>(StringComparer.Ordinal);
            long storedSum = 0;
            for (int i = 0; i < count; i++)
            {
                var e = file.Slice(ContainerHeader.Size + Entry * i, Entry);
                for (int k = 0; k < 4; k++) if (!SaveContainerWriter.IsKeyChar(e[k])) return ContainerReadStatus.CorruptHeader;
                var t = new TableEntry
                {
                    Key = new string(new[] { (char)e[0], (char)e[1], (char)e[2], (char)e[3] }),
                    Version = BinaryPrimitives.ReadUInt16LittleEndian(e.Slice(4)),
                    Flags = BinaryPrimitives.ReadUInt16LittleEndian(e.Slice(6)),
                    StoredLength = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(8)),
                    RawLength = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(12)),
                    Crc = BinaryPrimitives.ReadUInt32LittleEndian(e.Slice(16)),
                };
                if (!keys.Add(t.Key) || (t.Flags & ~(ushort)ChunkFlags.Deflate) != 0 || t.RawLength > SaveContainerWriter.MaxRawLength) return ContainerReadStatus.CorruptHeader;
                storedSum += t.StoredLength;
                table[i] = t;
            }
            if (requireWholeFile && storedSum != file.Length - tableEnd) return ContainerReadStatus.CorruptHeader;
            return ContainerReadStatus.Ok;
        }

        // 6. One chunk: CRC, then inflate when flagged. Any failure marks only this chunk corrupt.
        static RawChunk ReadChunk(ReadOnlySpan<byte> stored, in TableEntry e, Game.Data.Validation.ValidationReport report, string sourceLabel)
        {
            var chunk = new RawChunk { Key = e.Key, Version = e.Version, StoredFlags = (ChunkFlags)e.Flags };
            string problem = null;
            if (Crc32.Compute(stored) != e.Crc) problem = "CRC mismatch";
            else if ((e.Flags & (ushort)ChunkFlags.Deflate) == 0)
            {
                if (e.RawLength != e.StoredLength) problem = $"raw length {e.RawLength} ≠ stored length {e.StoredLength} on an uncompressed chunk";
                else chunk.Payload = stored.ToArray();
            }
            else
            {
                try { chunk.Payload = Inflate(stored, e.RawLength); if (chunk.Payload == null) problem = "inflated size differs from the table"; }
                catch (Exception ex) { problem = "inflate failed: " + ex.GetType().Name; }
            }
            if (problem != null)
            {
                chunk.Corrupt = true;
                chunk.Payload = null;
                report?.Error("PERS-E011", sourceLabel, e.Key, $"chunk {e.Key} is corrupt ({problem}); its participant resets to defaults");
            }
            return chunk;
        }

        /// <summary>Exactly rawLength bytes, or null when the stream holds more or fewer.</summary>
        static byte[] Inflate(ReadOnlySpan<byte> stored, uint rawLength)
        {
            var result = new byte[rawLength];
            using var ds = new DeflateStream(new MemoryStream(stored.ToArray(), writable: false), CompressionMode.Decompress);
            int read = 0;
            while (read < result.Length)
            {
                int n = ds.Read(result, read, result.Length - read);
                if (n <= 0) return null;
                read += n;
            }
            return ds.ReadByte() == -1 ? result : null;
        }
    }
}
