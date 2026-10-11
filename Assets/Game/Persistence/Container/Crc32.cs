using System;

namespace Game.Persistence.Container
{
    /// <summary>Implements SPEC-05 §4.5.</summary>
    public static class Crc32
    {
        public static uint Compute(ReadOnlySpan<byte> data) { throw new System.NotImplementedException("PERS-003"); }
        public static uint Append(uint crc, ReadOnlySpan<byte> data) { throw new System.NotImplementedException("PERS-003"); }
    }
}
