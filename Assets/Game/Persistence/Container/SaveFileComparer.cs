using System;
using System.Buffers.Binary;

namespace Game.Persistence.Container
{
    /// <summary>Compares two save files ignoring volatile fields (SPEC-05 decision 12): header bytes 12–19 (savedUtcTicks), 20–27
    /// (playtimeSeconds) and 32–35 (headerCrc32), the META table entry's flags, lengths and CRC, and the META payload. Used by tests and
    /// tools. Implements SPEC-05 §4.5.</summary>
    public static class SaveFileComparer
    {
        public static bool EqualIgnoringVolatile(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, out string firstDifference)
        {
            firstDifference = null;
            const int H = ContainerHeader.Size, E = SaveContainerWriter.TableEntrySize;
            if (a.Length < H || b.Length < H) return Plain(a, b, out firstDifference);

            for (int i = 0; i < H; i++)
            {
                if ((i >= 12 && i < 28) || i >= 32) continue;
                if (a[i] != b[i]) { firstDifference = $"offset {i} (header)"; return false; }
            }
            uint count = BinaryPrimitives.ReadUInt32LittleEndian(a.Slice(28));
            long tableEnd = H + (long)E * count;
            if (count > SaveContainerWriter.MaxChunks || tableEnd > a.Length || tableEnd > b.Length) return Plain(a, b, out firstDifference);

            long payA = tableEnd, payB = tableEnd;
            for (int i = 0; i < count; i++)
            {
                int at = H + E * i;
                var ea = a.Slice(at, E); var eb = b.Slice(at, E);
                bool meta = ea[0] == 'M' && ea[1] == 'E' && ea[2] == 'T' && ea[3] == 'A';
                int compared = meta ? 6 : E;                            // META: fourCC and version only
                for (int k = 0; k < compared; k++)
                    if (ea[k] != eb[k]) { firstDifference = $"offset {at + k} (table entry {i})"; return false; }
                uint lenA = BinaryPrimitives.ReadUInt32LittleEndian(ea.Slice(8)), lenB = BinaryPrimitives.ReadUInt32LittleEndian(eb.Slice(8));
                if (payA + lenA > a.Length || payB + lenB > b.Length) { firstDifference = $"offset {Math.Min(a.Length, b.Length)} (truncated payloads)"; return false; }
                if (!meta)
                {
                    var pa = a.Slice((int)payA, (int)lenA); var pb = b.Slice((int)payB, (int)lenB);
                    for (int k = 0; k < pa.Length; k++)
                        if (pa[k] != pb[k]) { firstDifference = $"offset {payA + k} (chunk {(char)ea[0]}{(char)ea[1]}{(char)ea[2]}{(char)ea[3]} payload +{k})"; return false; }
                }
                payA += lenA; payB += lenB;
            }
            if (payA != a.Length || payB != b.Length) { firstDifference = $"offset {Math.Min(payA, payB)} (trailing bytes)"; return false; }
            return true;
        }

        static bool Plain(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, out string firstDifference)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) if (a[i] != b[i]) { firstDifference = $"offset {i}"; return false; }
            firstDifference = a.Length == b.Length ? null : $"offset {n} (length {a.Length} vs {b.Length})";
            return firstDifference == null;
        }
    }
}
