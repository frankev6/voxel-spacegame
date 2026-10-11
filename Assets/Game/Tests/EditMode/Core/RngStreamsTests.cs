using System;
using System.Collections.Generic;
using Game.Core;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-003: RngStreams (SPEC-02 §5.2).</summary>
    public sealed class RngStreamsTests
    {
        [Test]
        public void DeriveSeed_Vectors_Match()
        {
            Assert.AreEqual(0x2802D39Du, RngStreams.DeriveSeed(12345, RngPurpose.GalaxyLayout, 0));
            Assert.AreEqual(0x75087B51u, RngStreams.DeriveSeed(12345, RngPurpose.StarSystemGeneration, 7));
            Assert.AreEqual(0xDDB0BEDEu, RngStreams.DeriveSeed(12345, RngPurpose.StarSystemGeneration, 8));
            Assert.AreEqual(0x45FC6D40u, RngStreams.DeriveSeed(0, RngPurpose.Loot, 0));
            Assert.AreEqual(0x017DA3BEu, RngStreams.DeriveSeed(0xDEADBEEFCAFEF00DUL, RngPurpose.WeaponSpread, 123456789));
        }

        [Test]
        public void For_EqualsRandomFromDerivedSeed()
        {
            var a = RngStreams.For(42, RngPurpose.Debris, 9);
            var b = new Unity.Mathematics.Random(RngStreams.DeriveSeed(42, RngPurpose.Debris, 9));
            for (int i = 0; i < 100; i++) Assert.AreEqual(b.NextUInt(), a.NextUInt(), $"value {i}");
        }

        [Test]
        public void Purposes_AreIndependent()
        {
            var seen = new HashSet<uint>();
            var pick = new Unity.Mathematics.Random(7);
            var purposes = (RngPurpose[])Enum.GetValues(typeof(RngPurpose));
            int made = 0;
            var used = new HashSet<(RngPurpose, ulong)>();
            while (made < 1000)
            {
                var p = purposes[pick.NextInt(1, purposes.Length)];
                ulong salt = pick.NextUInt(0, 5000);
                if (!used.Add((p, salt))) continue;
                var r = RngStreams.For(12345, p, salt);
                Assert.IsTrue(seen.Add(r.NextUInt()), $"{p}/{salt} collides with an earlier stream's first value");
                made++;
            }
        }

        [Test]
        public void PurposeNone_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => RngStreams.For(1, RngPurpose.None, 0));
            StringAssert.Contains("None", ex.Message);
        }

        [BurstCompile(CompileSynchronously = true)]
        struct EntityTickJob : IJob
        {
            public NativeArray<uint> Out;
            public void Execute()
            {
                for (int i = 0; i < Out.Length; i++) { var r = RngStreams.ForEntityTick((uint)(100 + i / 4), i % 4, 77); Out[i] = r.NextUInt(); }
            }
        }

        [Test]
        public void ForEntityTick_IsDeterministic_InBurst()
        {
            using var a = new NativeArray<uint>(32, Allocator.TempJob);
            using var b = new NativeArray<uint>(32, Allocator.TempJob);
            new EntityTickJob { Out = a }.Schedule().Complete();
            new EntityTickJob { Out = b }.Schedule().Complete();
            for (int i = 0; i < a.Length; i++)
            {
                Assert.AreEqual(a[i], b[i]);
                var managed = RngStreams.ForEntityTick((uint)(100 + i / 4), i % 4, 77);
                Assert.AreEqual(managed.NextUInt(), a[i], "Burst equals managed");
            }
            Assert.AreNotEqual(RngStreams.ForEntityTick(1, 2, 0).NextUInt(), RngStreams.ForEntityTick(2, 2, 0).NextUInt(), "tick changes the stream");
        }
    }
}
