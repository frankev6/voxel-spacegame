using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.2. 4096-bit solid mask as a value (structure storage, snapshots, collider batches), 512 bytes.
    /// Bit i = cell with local index i is solid (Ref != 0). Word w = i >> 6, bit b = i & 63.
    /// With i = x + 16y + 256z: w = (y >> 2) + 4z, b = x + 16*(y & 3).
    /// Row (y, z) — the 16 x-cells — is (ushort)(Words[(y>>2) + 4z] >> (16*(y&3))).</summary>
    public unsafe struct BrickMaskBits
    {
        public fixed ulong Words[64];
        public bool Get(int i)
        {
            fixed (ulong* words = Words) return (words[i >> 6] & (1UL << (i & 63))) != 0;
        }
        public void Set(int i, bool solid)
        {
            fixed (ulong* words = Words)
            {
                ulong bit = 1UL << (i & 63);
                if (solid) words[i >> 6] |= bit;
                else words[i >> 6] &= ~bit;
            }
        }
        public ushort Row(int y, int z)
        {
            fixed (ulong* words = Words) return (ushort)(words[(y >> 2) + 4 * z] >> (16 * (y & 3)));
        }
        public void SetRow(int y, int z, ushort bits)
        {
            int shift = 16 * (y & 3);
            fixed (ulong* words = Words)
            {
                int word = (y >> 2) + 4 * z;
                words[word] = (words[word] & ~(0xFFFFUL << shift)) | ((ulong)bits << shift);
            }
        }
        public int CountSolid()
        {
            int count = 0;
            fixed (ulong* words = Words) for (int i = 0; i < BrickMath.MaskWords; i++) count += math.countbits(words[i]);
            return count;
        }
        public bool IsEmpty()
        {
            fixed (ulong* words = Words) for (int i = 0; i < BrickMath.MaskWords; i++) if (words[i] != 0) return false;
            return true;
        }
        public bool IsFull()
        {
            fixed (ulong* words = Words) for (int i = 0; i < BrickMath.MaskWords; i++) if (words[i] != ulong.MaxValue) return false;
            return true;
        }
        public bool TryGetBounds(out int3 min, out int3 max)
        {
            min = new int3(BrickMath.Size); max = new int3(-1);
            fixed (ulong* words = Words)
                for (int w = 0; w < BrickMath.MaskWords; w++)
                {
                    ulong bits = words[w];
                    if (bits == 0) continue;
                    int z = w >> 2;
                    for (int r = 0; r < 4; r++)
                    {
                        uint row = (ushort)(bits >> (16 * r));
                        if (row == 0) continue;
                        int y = ((w & 3) << 2) + r;
                        min = math.min(min, new int3(math.tzcnt(row), y, z));
                        max = math.max(max, new int3(31 - math.lzcnt(row), y, z));
                    }
                }
            if (max.x >= 0) return true;
            min = max = default;
            return false;
        }
        public static BrickMaskBits Full()
        {
            var result = new BrickMaskBits();
            for (int i = 0; i < BrickMath.MaskWords; i++) result.Words[i] = ulong.MaxValue;
            return result;
        }
        public void CopyFrom(DynamicBuffer<BrickSolidMask> buffer)
        {
            if (buffer.Length != BrickMath.MaskWords) throw new System.ArgumentException("BrickSolidMask must have 64 words", nameof(buffer));
            fixed (ulong* words = Words) for (int i = 0; i < BrickMath.MaskWords; i++) words[i] = buffer[i].Bits;
        }
        public void CopyTo(DynamicBuffer<BrickSolidMask> buffer)
        {
            buffer.ResizeUninitialized(BrickMath.MaskWords);
            fixed (ulong* words = Words) for (int i = 0; i < BrickMath.MaskWords; i++) buffer[i] = new BrickSolidMask { Bits = words[i] };
        }
    }
}
