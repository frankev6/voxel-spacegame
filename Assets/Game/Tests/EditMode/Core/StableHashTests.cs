using System;
using System.Linq;
using System.Text;
using Game.Core;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-002: StableHash against the frozen vectors of SPEC-02 §5.1.</summary>
    public sealed class StableHashTests
    {
        const ulong P64 = 11400714785074694797UL;

        static string Long300()
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyz0123456789_";
            var sb = new StringBuilder(300);
            for (int i = 0; i < 300; i++) sb.Append(alphabet[i % 37]);
            return sb.ToString();
        }

        static byte[] SanityBuffer()
        {
            var buf = new byte[2367];
            ulong gen = 2654435761UL;
            unchecked { for (int i = 0; i < buf.Length; i++) { buf[i] = (byte)(gen >> 56); gen *= P64; } }
            return buf;
        }

        [Test]
        public void Hash64_StringVectors_Match()
        {
            var vectors = new (string, ulong)[]
            {
                ("", 0x2D06800538D394C2UL), ("a", 0xE6C632B61E964E1FUL), ("abc", 0x78AF5F94892F3950UL),
                ("block_hull", 0x208BCD2F5794080CUL), ("ship_frigate", 0x40C77FB94A14632EUL), ("item_fuel", 0xF1D231B0FE914DC8UL),
                ("faction_vestari", 0x4F914742E8527D13UL), ("config_game", 0xC55C4B09BD9D8A60UL), ("Kraków_ünïcödé", 0xD028B4D41FD1611BUL),
            };
            foreach (var (s, h) in vectors) Assert.AreEqual(h, StableHash.Hash64(s), $"\"{s}\"");
            Assert.AreEqual(StableHash.EmptyHash, StableHash.Hash64(""));
        }

        [Test]
        public void Hash64_LongString_Matches()
        {
            const string s97 = "block_thruster_large_mk2_with_a_long_suffix_exceeding_sixty_four_bytes_for_path_test_0123456789";
            Assert.AreEqual(0x5B6AC785A9FB5E77UL, StableHash.Hash64(s97));
            var s300 = Long300();
            Assert.AreEqual(0x7CD2B0369ED482C7UL, StableHash.Hash64(s300));
            var s600 = s300 + s300;   // above the 512-byte stack limit: the pooled path must agree with the span path
            Assert.AreEqual(StableHash.Hash64(Encoding.UTF8.GetBytes(s600)), StableHash.Hash64(s600));
        }

        static readonly int[] Lengths = { 0, 1, 6, 12, 24, 48, 80, 195, 403, 512, 2048, 2240, 2367 };

        [Test]
        public void Hash64_SanityBuffer_Seed0_Matches()
        {
            ulong[] expected = { 0x2D06800538D394C2UL, 0xC44BDFF4074EECDBUL, 0x27B56A84CD2D7325UL, 0xA713DAF0DFBB77E7UL, 0xA3FE70BF9D3510EBUL,
                                 0x397DA259ECBA1F11UL, 0xBCDEFBBB2C47C90AUL, 0xCD94217EE362EC3AUL, 0xCDEB804D65C6DEA4UL, 0x617E49599013CB6BUL,
                                 0xDD59E2C3A5F038E0UL, 0x6E73A90539CF2948UL, 0xCB37AEB9E5D361EDUL };
            var buf = SanityBuffer();
            for (int i = 0; i < Lengths.Length; i++) Assert.AreEqual(expected[i], StableHash.Hash64(new ReadOnlySpan<byte>(buf, 0, Lengths[i])), $"length {Lengths[i]}");
        }

        [Test]
        public void Hash64_SanityBuffer_SeedPrime64_Matches()
        {
            ulong[] expected = { 0xA8A6B918B2F0364AUL, 0x032BE332DD766EF8UL, 0x84589C116AB59AB9UL, 0xE7303E1B2336DE0EUL, 0x850E80FC35BDD690UL,
                                 0xADC2CBAA44ACC616UL, 0xC6DD0CB699532E73UL, 0xBA68003D370CB3D9UL, 0x6259F6ECFD6443FDUL, 0x3CE457DE14C27708UL,
                                 0x66F81670669ABABCUL, 0x757BA8487D1B5247UL, 0xD2DB3415B942B42AUL };
            var buf = SanityBuffer();
            for (int i = 0; i < Lengths.Length; i++) Assert.AreEqual(expected[i], StableHash.Hash64(new ReadOnlySpan<byte>(buf, 0, Lengths[i]), P64), $"length {Lengths[i]}");
        }

        [Test]
        public void Combine_Vectors_Match()
        {
            Assert.AreEqual(0xD0A66A65C7528968UL, StableHash.Combine(0, 0));
            Assert.AreEqual(0x07EE86C281446BEFUL, StableHash.Combine(1, 2));
            Assert.AreEqual(0xFB54CB6322BE3888UL, StableHash.Combine(2, 1));
            Assert.AreEqual(0x8504F741EA77AA46UL, StableHash.Combine(0x208BCD2F5794080CUL, 42));
            Assert.AreEqual(0x722A202F07A4B313UL, StableHash.Combine(ulong.MaxValue, ulong.MaxValue));
        }

        [Test]
        public void Combine_IsOrderDependent()
        {
            Assert.AreNotEqual(StableHash.Combine(1, 2), StableHash.Combine(2, 1));
            Assert.AreNotEqual(StableHash.Combine(5, 9), StableHash.Combine(9, 5));
            Assert.AreEqual(StableHash.Combine(5, 9), StableHash.Combine(5, 9), "deterministic");
        }

        [Test]
        public unsafe void AllOverloads_Agree()
        {
            foreach (var s in new[] { "", "block_hull", "Kraków_ünïcödé", "faction_vestari_with_a_name_long_enough_for_64" })
            {
                ulong expected = StableHash.Hash64(s);
                var bytes = Encoding.UTF8.GetBytes(s);
                Assert.AreEqual(expected, StableHash.Hash64(bytes), s);
                Assert.AreEqual(expected, StableHash.Hash64(bytes, 0), s);
                fixed (byte* p = bytes) { Assert.AreEqual(expected, StableHash.Hash64(p, bytes.Length), s); Assert.AreEqual(expected, StableHash.Hash64(p, bytes.Length, 0), s); }
                if (bytes.Length <= FixedString32Bytes.UTF8MaxLengthInBytes) Assert.AreEqual(expected, StableHash.Hash64(new FixedString32Bytes(s)), s);
                Assert.AreEqual(expected, StableHash.Hash64(new FixedString64Bytes(s)), s);
                Assert.AreEqual(expected, StableHash.Hash64(new FixedString128Bytes(s)), s);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => { unsafe { StableHash.Hash64((byte*)null, -1); } });
        }

        [BurstCompile(CompileSynchronously = true)]
        struct HashJob : IJob
        {
            public FixedString64Bytes Text;
            public NativeArray<ulong> Out;
            public void Execute()
            {
                Out[0] = StableHash.Hash64(Text);
                Out[1] = StableHash.Combine(Out[0], 42);
            }
        }

        [Test]
        public void BurstJob_MatchesManaged()
        {
            using var output = new NativeArray<ulong>(2, Allocator.TempJob);
            new HashJob { Text = new FixedString64Bytes("block_hull"), Out = output }.Schedule().Complete();
            Assert.AreEqual(0x208BCD2F5794080CUL, output[0]);
            Assert.AreEqual(0x8504F741EA77AA46UL, output[1]);
        }

        [Test]
        public void Hash64_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => StableHash.Hash64((string)null));
        }
    }
}
