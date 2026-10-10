namespace Game.Data.Blocks
{
    /// <summary>24-bit set of allowed orientations (bit o = orientation o allowed). Serializable. Implements SPEC-04 §4.2.</summary>
    [System.Serializable]
    public struct OrientationMask
    {
        public uint Bits;                                   // only bits 0..23 may be set
        public const uint AllBits = 0x00FFFFFF;
        public const uint HorizontalForwardBits = 0x0000FFFF; // orientations 0..15: forward ∈ {+Z,+X,-Z,-X}, any roll
        public const uint YawOnlyBits = (1u << 0) | (1u << 4) | (1u << 8) | (1u << 12);
        public static OrientationMask All => new OrientationMask { Bits = AllBits };
        public bool Allows(byte o) => o < 24 && (Bits & (1u << o)) != 0;
    }
}
