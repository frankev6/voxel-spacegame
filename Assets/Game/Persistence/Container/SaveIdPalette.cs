using System;
using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Core.Save;

namespace Game.Persistence.Container
{
    /// <summary>Save-wide ContentId palette, the IDPL chunk (SPEC-05 §4.3). Writer role: GetOrAdd hands out local indices in order of
    /// first use. Reader role: Get maps them back. Strings are kept as written, unvalidated, so old or unknown IDs survive a round
    /// trip. Local index 0xFFFF is "no ID". Implements the SPEC-02 palette interfaces (SPEC-05 §4.5, §4.10).</summary>
    public sealed class SaveIdPalette : ISaveIdPalette, ISaveIdPaletteReader
    {
        public const ushort None = 0xFFFF;
        /// <summary>Indices 0..65534.</summary>
        public const int MaxEntries = 0xFFFF;
        public const int MaxEntryBytes = 256;

        readonly List<ContentId> entries;
        readonly Dictionary<string, ushort> indexOf;

        SaveIdPalette(int capacity)
        {
            entries = new List<ContentId>(capacity);
            indexOf = new Dictionary<string, ushort>(capacity, StringComparer.Ordinal);
        }

        public static SaveIdPalette CreateEmpty() => new SaveIdPalette(64);

        /// <summary>Keeps the loaded entries at their indices (preserved chunks refer to them) and appends new ones.</summary>
        public static SaveIdPalette SeededFrom(SaveIdPalette loaded)
        {
            if (loaded == null) throw new ArgumentNullException(nameof(loaded));
            var p = new SaveIdPalette(loaded.entries.Count + 64);
            foreach (var id in loaded.entries) p.Append(id);
            return p;
        }

        /// <summary>varuint count, then count × (varuint byteLength, UTF-8 bytes). Throws SaveFormatException on a truncated or oversized
        /// payload, an entry longer than 256 bytes or trailing bytes.</summary>
        public static SaveIdPalette Parse(ReadOnlySpan<byte> idplPayload)
        {
            int pos = 0;
            uint count = ReadVarUInt(idplPayload, ref pos);
            if (count > MaxEntries) throw new SaveFormatException($"IDPL: {count} entries (max {MaxEntries})", 0);
            var p = new SaveIdPalette((int)count);
            var utf8 = new UTF8Encoding(false, false);
            for (uint i = 0; i < count; i++)
            {
                int at = pos;
                uint length = ReadVarUInt(idplPayload, ref pos);
                if (length > MaxEntryBytes) throw new SaveFormatException($"IDPL: entry {i} is {length} bytes (max {MaxEntryBytes})", at);
                if (pos + length > idplPayload.Length) throw new SaveFormatException($"IDPL: entry {i} runs past the end", at);
                p.Append(new ContentId(utf8.GetString(idplPayload.Slice(pos, (int)length))));
                pos += (int)length;
            }
            if (pos != idplPayload.Length) throw new SaveFormatException($"IDPL: {idplPayload.Length - pos} trailing bytes", pos);
            return p;
        }

        /// <summary>Same id → same index; ContentId.None → 0xFFFF. Throws SaveFormatException past 65,535 entries or for an id longer
        /// than 256 UTF-8 bytes (it could not be read back).</summary>
        public ushort GetOrAdd(in ContentId id)
        {
            if (id.IsNone) return None;
            if (indexOf.TryGetValue(id.Value, out var index)) return index;
            if (entries.Count >= MaxEntries) throw new SaveFormatException($"IDPL: more than {MaxEntries} distinct content ids", -1);
            if (Encoding.UTF8.GetByteCount(id.Value) > MaxEntryBytes) throw new SaveFormatException($"IDPL: content id '{id.Value.Substring(0, 32)}…' is longer than {MaxEntryBytes} bytes", -1);
            return Append(id);
        }

        /// <summary>0xFFFF → ContentId.None; an index ≥ Count throws SaveFormatException.</summary>
        public ContentId Get(ushort local)
        {
            if (local == None) return ContentId.None;
            if (local >= entries.Count) throw new SaveFormatException($"IDPL: local index {local} ≥ palette size {entries.Count}", -1);
            return entries[local];
        }

        public int Count => entries.Count;

        public byte[] Serialize()
        {
            var bytes = new List<byte>(16 + entries.Count * 16);
            WriteVarUInt(bytes, (uint)entries.Count);
            foreach (var id in entries)
            {
                var s = Encoding.UTF8.GetBytes(id.Value ?? "");
                WriteVarUInt(bytes, (uint)s.Length);
                bytes.AddRange(s);
            }
            return bytes.ToArray();
        }

        ushort Append(ContentId id)
        {
            var index = (ushort)entries.Count;
            entries.Add(id);
            if (!id.IsNone && !indexOf.ContainsKey(id.Value)) indexOf.Add(id.Value, index);   // a duplicate keeps its first index
            return index;
        }

        static void WriteVarUInt(List<byte> bytes, uint value)
        {
            while (value >= 0x80) { bytes.Add((byte)(value | 0x80)); value >>= 7; }
            bytes.Add((byte)value);
        }

        static uint ReadVarUInt(ReadOnlySpan<byte> data, ref int pos)
        {
            uint result = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                if (pos >= data.Length) throw new SaveFormatException("IDPL: truncated varuint", pos);
                byte b = data[pos++];
                if (shift == 28 && b > 0x0F) throw new SaveFormatException("IDPL: varuint overflows 32 bits", pos - 1);
                result |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0) return result;
            }
            throw new SaveFormatException("IDPL: varuint longer than 5 bytes", pos);
        }
    }
}
