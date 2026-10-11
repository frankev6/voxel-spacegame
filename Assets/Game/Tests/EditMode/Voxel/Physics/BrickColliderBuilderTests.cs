using System;
using System.Diagnostics;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Voxel.Physics
{
    /// <summary>PHY-005 brick geometry, Burst construction, timing and temporary-child ownership.</summary>
    public sealed class BrickColliderBuilderTests
    {
        [BurstCompile(CompileSynchronously = true)]
        struct CoverageJob : IJob
        {
            public BrickMaskBits Mask;
            public int Cap;
            public float Scale;
            public NativeReference<int> Failure;
            public NativeReference<int> Level;
            public void Execute()
            {
                var boxes = new NativeList<BrickBox>(4096, Allocator.Temp);
                Level.Value = BrickBoxMerger.Merge(Mask, Cap, ref boxes);
                var blob = BrickColliderBuilder.Build(Mask, Scale, Cap, 0.25f,
                    PhysicsLayers.Grid, PhysicsLayers.GridMaterial, ref boxes, out int count);
                if (!blob.IsCreated || count > Cap) { Failure.Value = 1; boxes.Dispose(); return; }
                if (count > 1)
                {
                    unsafe
                    {
                        if (blob.Value.Type != ColliderType.Compound || ((CompoundCollider*)blob.GetUnsafePtr())->NumChildren != count)
                            Failure.Value = 2;
                    }
                }
                // Quarter-cell lattice is offset from integer box boundaries; add every cell-centre ray too.
                // Both directions validate the exposed front and back of each column.
                for (int axis = 0; axis < 3; axis++)
                for (int u = 0; u < 80; u++) for (int v = 0; v < 80; v++)
                {
                    float a = u < 64 ? 0.125f + u * 0.25f : u - 64 + 0.5f;
                    float b = v < 64 ? 0.125f + v * 0.25f : v - 64 + 0.5f;
                    int first = -1, last = -1;
                    for (int t = 0; t < 16; t++)
                    {
                        int3 c = axis == 0 ? new int3(t, (int)a, (int)b) : axis == 1 ? new int3((int)a, t, (int)b) : new int3((int)a, (int)b, t);
                        if (Mask.Get(c.x + 16 * c.y + 256 * c.z)) { if (first < 0) first = t; last = t; }
                    }
                    for (int direction = 0; direction < 2; direction++)
                    {
                        float3 start = axis == 0 ? new float3(-1, a, b) : axis == 1 ? new float3(a, -1, b) : new float3(a, b, -1);
                        float3 end = start; end[axis] = 17;
                        if (direction != 0) { var swap = start; start = end; end = swap; }
                        bool hit = blob.Value.CastRay(new RaycastInput { Start = start * Scale, End = end * Scale, Filter = PhysicsLayers.ProjectileQuery }, out var result);
                        if (first >= 0 && !hit) Failure.Value = 3;
                        if (Level.Value == 0 && first < 0 && hit) Failure.Value = 4;
                        // At cell-centre cross-sections bevels leave the entry face exact.
                        if (Level.Value == 0 && first >= 0 && hit && u >= 64 && v >= 64)
                        {
                            float expected = (direction == 0 ? first : last + 1) * Scale;
                            if (math.abs(result.Position[axis] - expected) > Scale * 1e-4f) Failure.Value = 5;
                        }
                    }
                }
                // Short segments lie entirely in one cell, so every occupied centre is checked even inside a column.
                for (int i = 0; i < 4096; i++)
                {
                    float3 centre = new float3(i % 16, i / 16 % 16, i / 256) + 0.5f;
                    bool hit = blob.Value.CastRay(new RaycastInput { Start = (centre - new float3(0.2f, 0, 0)) * Scale,
                        End = (centre + new float3(0.2f, 0, 0)) * Scale, Filter = PhysicsLayers.ProjectileQuery });
                    if (Mask.Get(i) && !hit) Failure.Value = 6;
                    if (Level.Value == 0 && !Mask.Get(i) && hit) Failure.Value = 7;
                }
                blob.Dispose(); boxes.Dispose();
            }
        }
        [BurstCompile(CompileSynchronously = true)]
        struct BuildJob : IJob
        {
            public BrickMaskBits Mask;
            public NativeReference<BlobAssetReference<Collider>> Result;
            public void Execute()
            {
                var scratch = new NativeList<BrickBox>(4096, Allocator.Temp);
                Result.Value = BrickColliderBuilder.Build(Mask, 1, 64, 0.1f, PhysicsLayers.Grid, PhysicsLayers.GridMaterial, ref scratch, out _);
                scratch.Dispose();
            }
        }
        static BrickMaskBits Mask(int sample)
        {
            var random = new Random(0x51DEu + (uint)sample);
            int density = (sample % 3) switch { 0 => 1, 1 => 10, _ => 50 };
            var mask = new BrickMaskBits();
            for (int i = 0; i < 4096; i++) if (random.NextInt(100) < density) mask.Set(i, true);
            return mask;
        }
        [Test]
        public void SolidCells_AlwaysHit()
        {
            using var failure = new NativeReference<int>(Allocator.TempJob);
            using var level = new NativeReference<int>(Allocator.TempJob);
            int seen = 0;
            for (int sample = 0; sample < 9; sample++) foreach (int cap in new[] { 4096, 64, 8, 1 })
            {
                new CoverageJob { Mask = Mask(sample), Cap = cap, Scale = sample % 2 == 0 ? 0.3f : 2,
                    Failure = failure, Level = level }.Schedule().Complete();
                Assert.AreEqual(0, failure.Value, $"sample={sample}, cap={cap}, level={level.Value}");
                seen |= 1 << level.Value;
            }
            Assert.AreEqual(15, seen, "Exercise all four merge levels");
            // Report construction alone; the complete reference-settings gate remains PHY-019.
            using var result = new NativeReference<BlobAssetReference<Collider>>(Allocator.TempJob);
            var build = new BuildJob { Mask = Mask(2), Result = result };
            for (int i = 0; i < 4; i++) { build.Run(); result.Value.Dispose(); }
            var samples = new double[101];
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); build.Run();
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1e6 / Stopwatch.Frequency; result.Value.Dispose();
            }
            Array.Sort(samples);
            TestContext.WriteLine($"PHY-005 noise50 build: median {samples[50]:F3} us; p95 {samples[95]:F3} us (Run overhead included; current test safety settings)");
        }
        [Test]
        public void Empty_ReturnsNull()
        {
            var scratch = new NativeList<BrickBox>(Allocator.Temp);
            try
            {
                scratch.Add(default);
                var blob = BrickColliderBuilder.Build(default, 1, 64, 0.1f, PhysicsLayers.Grid, PhysicsLayers.GridMaterial, ref scratch, out int count);
                Assert.IsFalse(blob.IsCreated); Assert.AreEqual(0, count); Assert.AreEqual(0, scratch.Length);
            }
            finally { scratch.Dispose(); }
        }
        [Test]
        public unsafe void OneBox_IsBoxCollider()
        {
            var mask = new BrickMaskBits(); mask.Set(7 + 16 * 8 + 256 * 9, true);
            var scratch = new NativeList<BrickBox>(Allocator.Temp);
            var blob = BrickColliderBuilder.Build(mask, 2, 64, 0.25f, PhysicsLayers.EditorPreview, PhysicsLayers.EditorPreviewMaterial, ref scratch, out int count);
            try
            {
                Assert.AreEqual(1, count); Assert.AreEqual(ColliderType.Box, blob.Value.Type);
                var box = (BoxCollider*)blob.GetUnsafePtr();
                Assert.AreEqual(new float3(15, 17, 19), box->Center); Assert.AreEqual(new float3(2), box->Size);
                Assert.AreEqual(0.5f, box->BevelRadius);
                Assert.AreEqual(PhysicsLayers.EditorPreview, box->GetCollisionFilter());
                Assert.AreEqual(CollisionResponsePolicy.None, box->Material.CollisionResponse);
            }
            finally { blob.Dispose(); scratch.Dispose(); }
        }
        [Test]
        public void ChildrenDisposed_NoLeak()
        {
            var previous = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try
            {
                using var result = new NativeReference<BlobAssetReference<Collider>>(Allocator.TempJob);
                var build = new BuildJob { Mask = Mask(0), Result = result };
                build.Run(); result.Value.Dispose(); UnsafeUtility.CheckForLeaks();
                for (int i = 0; i < 32; i++) { build.Schedule().Complete(); result.Value.Dispose(); }
                Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
            }
            finally { NativeLeakDetection.Mode = previous; }
        }
    }
}
