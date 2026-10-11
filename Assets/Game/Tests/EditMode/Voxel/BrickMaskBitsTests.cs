using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-004: mask rows, word-scan bounds and buffer copies against independent bit references (SPEC-10 §4.2).</summary>
    public sealed class BrickMaskBitsTests
    {
        static BrickMaskBits RandomMask(ref Random random, int sample, out bool[] reference)
        {
            reference = new bool[4096];
            var mask = new BrickMaskBits();
            int3 min = random.NextInt3(0, 16), max = random.NextInt3(min, new int3(16));
            int density = random.NextInt(1, 33);
            for (int z = 0; z < 16; z++)
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        int i = x + 16 * y + 256 * z;
                        bool solid = sample % 17 != 0 && math.all(new int3(x, y, z) >= min) && math.all(new int3(x, y, z) <= max) && random.NextInt(32) < density;
                        reference[i] = solid; mask.Set(i, solid);
                    }
            return mask;
        }

        [Test]
        public void Row_MatchesBits()
        {
            var random = new Random(0xBACCAu);
            for (int sample = 0; sample < 64; sample++)
            {
                var mask = new BrickMaskBits();
                var rows = new ushort[256];
                for (int z = 0; z < 16; z++)
                    for (int y = 0; y < 16; y++) { rows[y + 16 * z] = (ushort)random.NextUInt(); mask.SetRow(y, z, rows[y + 16 * z]); }
                for (int z = 0; z < 16; z++)
                    for (int y = 0; y < 16; y++)
                    {
                        ushort expected = rows[y + 16 * z];
                        Assert.AreEqual(expected, mask.Row(y, z));
                        ushort bits = 0;
                        for (int x = 0; x < 16; x++) if (mask.Get(x + 16 * y + 256 * z)) bits |= (ushort)(1 << x);
                        Assert.AreEqual(expected, bits);
                    }
            }
        }

        [Test]
        public void Bounds_MatchBruteForce()
        {
            var random = new Random(0xBEACEu);
            for (int sample = 0; sample < 1000; sample++)
            {
                var mask = RandomMask(ref random, sample, out var reference);
                int3 expectedMin = new int3(16), expectedMax = new int3(-1);
                for (int i = 0; i < reference.Length; i++) if (reference[i])
                {
                    var cell = new int3(i % 16, i / 16 % 16, i / 256);
                    expectedMin = math.min(expectedMin, cell); expectedMax = math.max(expectedMax, cell);
                }
                bool found = mask.TryGetBounds(out var min, out var max);
                Assert.AreEqual(expectedMax.x >= 0, found);
                if (found) { Assert.AreEqual(expectedMin, min); Assert.AreEqual(expectedMax, max); }
            }
        }

        [Test]
        public void Count_MatchesBruteForce()
        {
            var random = new Random(0xC011u);
            for (int sample = 0; sample < 1000; sample++)
            {
                var mask = RandomMask(ref random, sample, out var reference);
                int count = 0; foreach (bool bit in reference) if (bit) count++;
                Assert.AreEqual(count, mask.CountSolid());
            }
        }

        [Test]
        public void FullAndEmpty()
        {
            var empty = new BrickMaskBits();
            Assert.IsTrue(empty.IsEmpty()); Assert.IsFalse(empty.IsFull()); Assert.AreEqual(0, empty.CountSolid());
            Assert.IsFalse(empty.TryGetBounds(out _, out _));
            var full = BrickMaskBits.Full();
            Assert.IsFalse(full.IsEmpty()); Assert.IsTrue(full.IsFull()); Assert.AreEqual(4096, full.CountSolid());
            Assert.IsTrue(full.TryGetBounds(out var min, out var max)); Assert.AreEqual(int3.zero, min); Assert.AreEqual(new int3(15), max);
            for (int i = 0; i < 4096; i++) full.Set(i, false);
            Assert.IsTrue(full.IsEmpty()); Assert.AreEqual(0, full.CountSolid());
        }

        [Test]
        public void BufferRoundTrip()
        {
            using var world = new World("brick-mask-copy");
            var entity = world.EntityManager.CreateEntity(typeof(BrickSolidMask));
            var buffer = world.EntityManager.GetBuffer<BrickSolidMask>(entity);
            var random = new Random(0xB0FFE2u);
            for (int sample = 0; sample < 64; sample++)
            {
                var mask = RandomMask(ref random, sample, out _);
                mask.CopyTo(buffer); Assert.AreEqual(64, buffer.Length);
                var restored = new BrickMaskBits(); restored.CopyFrom(buffer);
                for (int z = 0; z < 16; z++) for (int y = 0; y < 16; y++) Assert.AreEqual(mask.Row(y, z), restored.Row(y, z));
            }
        }
    }
}
