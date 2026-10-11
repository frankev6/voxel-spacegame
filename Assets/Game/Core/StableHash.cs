using System;
using System.Buffers;
using System.Text;
using Unity.Burst;
using Unity.Collections;

namespace Game.Core
{
    /// <summary>xxHash3-64 stable hashing (SPEC-02 §4.2, §5.1; decision 4). Never use GetHashCode for persisted or simulated data.
    /// The result of Unity.Collections.xxHash3.Hash64 is packed as ((ulong)r.y &lt;&lt; 32) | r.x; strings hash their UTF-8 bytes with seed 0.
    /// The behaviour is frozen by the test vectors of §5.1.</summary>
    public static class StableHash
    {
        public const ulong EmptyHash = 0x2D06800538D394C2UL;          // Hash64(empty input)
        const int StackLimit = 512;                                    // UTF-8 bytes hashed from a stackalloc buffer; longer strings rent from ArrayPool

        /// <summary>UTF-8, seed 0. ArgumentNullException on null. Managed only; no GC allocation up to 512 UTF-8 bytes.</summary>
        public static unsafe ulong Hash64(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (!BitConverter.IsLittleEndian) throw new PlatformNotSupportedException("StableHash requires a little-endian platform.");
            int n = Encoding.UTF8.GetByteCount(value);
            if (n <= StackLimit)
            {
                Span<byte> buf = stackalloc byte[n];
                Encoding.UTF8.GetBytes(value.AsSpan(), buf);
                return Hash64(buf, 0);
            }
            byte[] rented = ArrayPool<byte>.Shared.Rent(n);
            try
            {
                int written = Encoding.UTF8.GetBytes(value, 0, value.Length, rented, 0);
                return Hash64(new ReadOnlySpan<byte>(rented, 0, written), 0);
            }
            finally { ArrayPool<byte>.Shared.Return(rented); }
        }

        public static ulong Hash64(ReadOnlySpan<byte> data) => Hash64(data, 0);     // seed 0. Managed only

        public static unsafe ulong Hash64(ReadOnlySpan<byte> data, ulong seed)
        {
            fixed (byte* p = data) return Hash64(p, data.Length, seed);              // an empty span gives null; xxHash3 never reads it for length 0
        }

        public static unsafe ulong Hash64(byte* data, int length) => Hash64(data, length, 0);   // Burst-compatible; length >= 0

        public static unsafe ulong Hash64(byte* data, int length, ulong seed)
        {
            CheckLength(length);
            var r = xxHash3.Hash64(data, length, seed);
            return ((ulong)r.y << 32) | r.x;
        }

        // Burst-compatible: the UTF-8 content bytes only, identical to hashing the equivalent string.
        public static unsafe ulong Hash64(in FixedString32Bytes text) => Hash64(text.GetUnsafePtr(), text.Length, 0);
        public static unsafe ulong Hash64(in FixedString64Bytes text) => Hash64(text.GetUnsafePtr(), text.Length, 0);
        public static unsafe ulong Hash64(in FixedString128Bytes text) => Hash64(text.GetUnsafePtr(), text.Length, 0);

        struct Pair { public ulong A; public ulong B; }

        /// <summary>Burst-compatible; order matters: Combine(a,b) != Combine(b,a). Hash64 of the 16 bytes a, b (little-endian), seed 0.</summary>
        public static unsafe ulong Combine(ulong a, ulong b)
        {
            var pair = new Pair { A = a, B = b };
            return Hash64((byte*)&pair, 16, 0);
        }

        [BurstDiscard]   // managed callers get an exception; Burst callers never pass a negative length (undefined there)
        static void CheckLength(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), length, "length must not be negative");
        }
    }
}
