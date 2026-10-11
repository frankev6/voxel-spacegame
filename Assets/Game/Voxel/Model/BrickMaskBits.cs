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
        public bool Get(int i) { throw new System.NotImplementedException("VOX-004"); }
        public void Set(int i, bool solid) { throw new System.NotImplementedException("VOX-004"); }
        public ushort Row(int y, int z) { throw new System.NotImplementedException("VOX-004"); }
        public void SetRow(int y, int z, ushort bits) { throw new System.NotImplementedException("VOX-004"); }
        public int CountSolid() { throw new System.NotImplementedException("VOX-004"); }                                 // math.countbits over 64 words
        public bool IsEmpty() { throw new System.NotImplementedException("VOX-004"); }
        public bool IsFull() { throw new System.NotImplementedException("VOX-004"); }
        public bool TryGetBounds(out int3 min, out int3 max) { throw new System.NotImplementedException("VOX-004"); }    // inclusive local bounds; false if empty
        public static BrickMaskBits Full() { throw new System.NotImplementedException("VOX-004"); }
        public void CopyFrom(DynamicBuffer<BrickSolidMask> buffer) { throw new System.NotImplementedException("VOX-004"); }   // buffer.Length must be 64
        public void CopyTo(DynamicBuffer<BrickSolidMask> buffer) { throw new System.NotImplementedException("VOX-004"); }     // resizes to 64
    }
}
