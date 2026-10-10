namespace Game.Core
{
    /// <summary>xxHash3-64 stable hashing (SPEC-02 §4.2, §5.1). Never use GetHashCode for persisted or simulated data.</summary>
    public static class StableHash
    {
        public const ulong EmptyHash = 0x2D06800538D394C2UL;          // Hash64(empty input)
        public static ulong Hash64(string value) { throw new System.NotImplementedException("CORE-002"); }                       // UTF-8, seed 0. ArgumentNullException on null. Managed only
        public static ulong Hash64(System.ReadOnlySpan<byte> data) { throw new System.NotImplementedException("CORE-002"); }     // seed 0. Managed only
        public static ulong Hash64(System.ReadOnlySpan<byte> data, ulong seed) { throw new System.NotImplementedException("CORE-002"); }
        public static unsafe ulong Hash64(byte* data, int length) { throw new System.NotImplementedException("CORE-002"); }      // Burst-compatible; length >= 0
        public static unsafe ulong Hash64(byte* data, int length, ulong seed) { throw new System.NotImplementedException("CORE-002"); }
        public static ulong Hash64(in Unity.Collections.FixedString32Bytes text) { throw new System.NotImplementedException("CORE-002"); }   // Burst-compatible; hashes the UTF-8 content bytes only
        public static ulong Hash64(in Unity.Collections.FixedString64Bytes text) { throw new System.NotImplementedException("CORE-002"); }
        public static ulong Hash64(in Unity.Collections.FixedString128Bytes text) { throw new System.NotImplementedException("CORE-002"); }
        public static ulong Combine(ulong a, ulong b) { throw new System.NotImplementedException("CORE-002"); }                  // Burst-compatible; order matters: Combine(a,b) != Combine(b,a)
    }
}
