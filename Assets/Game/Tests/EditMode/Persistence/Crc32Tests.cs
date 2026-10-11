using System;
using System.Text;
using Game.Persistence.Container;
using NUnit.Framework;

namespace Game.Tests.EditMode.Persistence
{
    /// <summary>PERS-003: CRC-32 IEEE (SPEC-05 §4.2).</summary>
    public sealed class Crc32Tests
    {
        [Test]
        public void KnownVector()
        {
            Assert.AreEqual(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
            Assert.AreEqual(0u, Crc32.Compute(ReadOnlySpan<byte>.Empty));
            Assert.AreEqual(0x414FA339u, Crc32.Compute(Encoding.ASCII.GetBytes("The quick brown fox jumps over the lazy dog")));
        }

        [Test]
        public void Append_MatchesWhole_AndSliceBy8MatchesBytewise()
        {
            var rng = new Random(7);
            for (int len = 0; len < 100; len++)
            {
                var data = new byte[len]; rng.NextBytes(data);
                uint whole = Crc32.Compute(data);
                for (int split = 0; split <= len; split += Math.Max(1, len / 5))
                    Assert.AreEqual(whole, Crc32.Append(Crc32.Compute(data.AsSpan(0, split)), data.AsSpan(split)), $"len {len} split {split}");
                Assert.AreEqual(Bytewise(data), whole, $"len {len}");
            }
        }

        static uint Bytewise(byte[] data)
        {
            uint c = 0xFFFFFFFF;
            foreach (var b in data) { c ^= b; for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1; }
            return ~c;
        }
    }
}
