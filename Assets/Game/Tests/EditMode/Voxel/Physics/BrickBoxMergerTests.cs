using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-004: exact covers, conservative levels and Burst/managed ordering (SPEC-12 §5.2).</summary>
    public sealed class BrickBoxMergerTests
    {
        static BrickMaskBits RandomMask(ref Random random, int sample)
        {
            int density = (sample % 5) switch { 0 => 1, 1 => 10, 2 => 50, 3 => 90, _ => 100 };
            var mask = new BrickMaskBits();
            for (int i = 0; i < 4096; i++) if (random.NextInt(100) < density) mask.Set(i, true);
            return mask;
        }
        static BrickMaskBits Structured(int kind)
        {
            var mask = new BrickMaskBits();
            for (int z = 0; z < 16; z++) for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
            {
                bool solid = kind switch
                {
                    0 => true, 1 => z < 4, 2 => x < 2 || y < 2,
                    3 => ((x + y + z) & 1) == 0, 4 => x == 7 && y == 8 && z == 9,
                    5 => x == 0 || y == 0 || z == 0 || x == 15 || y == 15 || z == 15,
                    _ => false,
                };
                if (solid) mask.Set(x + 16 * y + 256 * z, true);
            }
            return mask;
        }
        static void Cover(in BrickMaskBits source, NativeList<BrickBox> boxes, bool exact)
        {
            var covered = new bool[4096];
            for (int i = 0; i < boxes.Length; i++)
            {
                var b = boxes[i];
                if (b.MinX > b.MaxX || b.MinY > b.MaxY || b.MinZ > b.MaxZ || b.MaxX > 15 || b.MaxY > 15 || b.MaxZ > 15)
                    Assert.Fail("Invalid box bounds");
                for (int z = b.MinZ; z <= b.MaxZ; z++) for (int y = b.MinY; y <= b.MaxY; y++) for (int x = b.MinX; x <= b.MaxX; x++)
                {
                    int cell = x + 16 * y + 256 * z;
                    if (covered[cell]) Assert.Fail("Boxes overlap");
                    covered[cell] = true;
                }
            }
            for (int i = 0; i < 4096; i++)
                if ((source.Get(i) && !covered[i]) || (exact && !source.Get(i) && covered[i])) Assert.Fail("Cover differs from mask");
        }

        [Test]
        public void Exact_DisjointAndCoversMask_RandomMasks()
        {
            var boxes = new NativeList<BrickBox>(4096, Allocator.Temp);
            try
            {
                var random = new Random(0xB04E5u);
                for (int i = 0; i < 10000; i++) { var mask = RandomMask(ref random, i); BrickBoxMerger.MergeExact(mask, ref boxes); Cover(mask, boxes, true); }
                for (int i = 0; i < 7; i++) { var mask = Structured(i); BrickBoxMerger.MergeExact(mask, ref boxes); Cover(mask, boxes, true); }
            }
            finally { boxes.Dispose(); }
        }

        [Test]
        public void Coarse_IsSuperset()
        {
            var boxes = new NativeList<BrickBox>(4096, Allocator.Temp);
            try
            {
                var random = new Random(0xC0A25Eu);
                for (int i = 0; i < 1000 + 7; i++)
                {
                    var mask = i < 1000 ? RandomMask(ref random, i) : Structured(i - 1000);
                    BrickBoxMerger.MergeCoarse(mask, 2, ref boxes); Cover(mask, boxes, false);
                    BrickBoxMerger.MergeCoarse(mask, 4, ref boxes); Cover(mask, boxes, false); Assert.LessOrEqual(boxes.Length, 64);
                }
            }
            finally { boxes.Dispose(); }
        }

        [Test]
        public void Merge_RespectsMaxBoxes()
        {
            var boxes = new NativeList<BrickBox>(4096, Allocator.Temp);
            try
            {
                var random = new Random(0xCA95u);
                for (int i = 0; i < 1000; i++)
                {
                    var mask = RandomMask(ref random, i); int cap = 1 + i % 64;
                    int level = BrickBoxMerger.Merge(mask, cap, ref boxes);
                    Assert.LessOrEqual(boxes.Length, cap); Cover(mask, boxes, level == 0);
                    if (level == 3)
                    {
                        Assert.AreEqual(1, boxes.Length); mask.TryGetBounds(out var min, out var max); var b = boxes[0];
                        Assert.AreEqual(min, new int3(b.MinX, b.MinY, b.MinZ)); Assert.AreEqual(max, new int3(b.MaxX, b.MaxY, b.MaxZ));
                    }
                }
                var separated = new BrickMaskBits(); separated.Set(0, true); separated.Set(4095, true);
                Assert.AreEqual(3, BrickBoxMerger.Merge(separated, 1, ref boxes));
                Assert.AreEqual(15, boxes[0].MaxX); Assert.AreEqual(0, boxes[0].MinX);
                Assert.AreEqual(0, BrickBoxMerger.Merge(default, 64, ref boxes)); Assert.AreEqual(0, boxes.Length);
            }
            finally { boxes.Dispose(); }
        }

        [Test] public void Full_IsOneBox() => OneBox(0, 15);
        [Test] public void Slab_IsOneBox() => OneBox(1, 3);
        static void OneBox(int kind, int maxZ)
        {
            var boxes = new NativeList<BrickBox>(Allocator.Temp);
            try
            {
                Assert.AreEqual(1, BrickBoxMerger.MergeExact(Structured(kind), ref boxes)); var b = boxes[0];
                Assert.AreEqual(int3.zero, new int3(b.MinX, b.MinY, b.MinZ)); Assert.AreEqual(new int3(15, 15, maxZ), new int3(b.MaxX, b.MaxY, b.MaxZ));
            }
            finally { boxes.Dispose(); }
        }
        [Test]
        public void Checkerboard_Coarsens()
        {
            var boxes = new NativeList<BrickBox>(4096, Allocator.Temp);
            try
            {
                var mask = Structured(3); Assert.AreEqual(2048, BrickBoxMerger.MergeExact(mask, ref boxes));
                Assert.GreaterOrEqual(BrickBoxMerger.Merge(mask, 64, ref boxes), 1); Assert.LessOrEqual(boxes.Length, 64); Cover(mask, boxes, false);
            }
            finally { boxes.Dispose(); }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct MergeJob : IJob
        {
            public BrickMaskBits Mask; public int Mode; public NativeList<BrickBox> Boxes;
            public void Execute()
            {
                if (Mode == 0) BrickBoxMerger.MergeExact(Mask, ref Boxes);
                else if (Mode == 1) BrickBoxMerger.MergeCoarse(Mask, 2, ref Boxes);
                else if (Mode == 2) BrickBoxMerger.MergeCoarse(Mask, 4, ref Boxes);
                else BrickBoxMerger.Merge(Mask, 1, ref Boxes);
            }
        }
        [Test]
        public void Deterministic_BurstVsManaged()
        {
            var managed = new NativeList<BrickBox>(4096, Allocator.TempJob);
            var burst = new NativeList<BrickBox>(4096, Allocator.TempJob);
            try
            {
                var random = new Random(0xDE7Eu);
                for (int i = 0; i < 107; i++) for (int mode = 0; mode < 4; mode++)
                {
                    var mask = i < 100 ? RandomMask(ref random, i) : Structured(i - 100);
                    var job = new MergeJob { Mask = mask, Mode = mode, Boxes = managed }; job.Execute();
                    new MergeJob { Mask = mask, Mode = mode, Boxes = burst }.Run();
                    CollectionAssert.AreEqual(managed.AsArray().ToArray(), burst.AsArray().ToArray());
                    job.Execute(); // Same mask and mode reproduce the same ordered byte-sized boxes.
                    CollectionAssert.AreEqual(managed.AsArray().ToArray(), burst.AsArray().ToArray());
                }
            }
            finally { managed.Dispose(); burst.Dispose(); }
        }
    }
}
