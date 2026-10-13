namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct BlockRecord : System.IEquatable<BlockRecord>
    {
        public Unity.Mathematics.int3 Origin; // grid cell of block-local (0,0,0) after rotation
        public ushort Block;                 // BlockIndex
        public byte Orient;                  // 0..23
        public byte Color;                   // palette colour index
        public int DamageOffset;             // -1 = pristine; otherwise offset in the owner's damage list

        // Damage is deliberately excluded from placed-block identity (SPEC-51 §4.2).
        public bool Equals(BlockRecord o) => Origin.Equals(o.Origin) && Block == o.Block && Orient == o.Orient && Color == o.Color;
        public override bool Equals(object obj) => obj is BlockRecord other && Equals(other);
        public override int GetHashCode() => (int)Game.Core.StableHash.Combine((ulong)Origin.GetHashCode(),
            ((ulong)Block << 16) | ((ulong)Orient << 8) | Color); // in-memory only; never simulation/save identity
    }
}
