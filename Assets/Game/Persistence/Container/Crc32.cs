using System;

namespace Game.Persistence.Container
{
    /// <summary>CRC-32 IEEE 802.3: reflected polynomial 0xEDB88320, initial 0xFFFFFFFF, final XOR 0xFFFFFFFF; Compute("123456789") =
    /// 0xCBF43926. Slice-by-8 tables. Thread-safe. Implements SPEC-05 §4.2, §4.5.</summary>
    public static class Crc32
    {
        [Game.Core.StaticStateOk("constant table, never written after the static constructor")] static readonly uint[] Table = BuildTable();       // 8 × 256 entries

        static uint[] BuildTable()
        {
            var t = new uint[8 * 256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            for (int i = 0; i < 256; i++)
                for (int s = 1; s < 8; s++)
                    t[s * 256 + i] = (t[(s - 1) * 256 + i] >> 8) ^ t[t[(s - 1) * 256 + i] & 0xFF];
            return t;
        }

        public static uint Compute(ReadOnlySpan<byte> data) => Append(0, data);

        /// <summary>Continues a finished CRC over more bytes: Append(Compute(a), b) == Compute(a + b).</summary>
        public static uint Append(uint crc, ReadOnlySpan<byte> data)
        {
            var t = Table;
            uint c = ~crc;
            int i = 0, n = data.Length;
            for (; i + 8 <= n; i += 8)
            {
                uint lo = c ^ (uint)(data[i] | data[i + 1] << 8 | data[i + 2] << 16 | data[i + 3] << 24);
                uint hi = (uint)(data[i + 4] | data[i + 5] << 8 | data[i + 6] << 16 | data[i + 7] << 24);
                c = t[7 * 256 + (lo & 0xFF)] ^ t[6 * 256 + ((lo >> 8) & 0xFF)] ^ t[5 * 256 + ((lo >> 16) & 0xFF)] ^ t[4 * 256 + (lo >> 24)]
                  ^ t[3 * 256 + (hi & 0xFF)] ^ t[2 * 256 + ((hi >> 8) & 0xFF)] ^ t[1 * 256 + ((hi >> 16) & 0xFF)] ^ t[hi >> 24];
            }
            for (; i < n; i++) c = t[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            return ~c;
        }
    }
}
