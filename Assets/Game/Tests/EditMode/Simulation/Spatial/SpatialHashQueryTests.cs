using System;
using System.Collections.Generic;
using Game.Simulation.Spatial;
using NUnit.Framework;
using Unity.Collections;
using Unity.Burst;
using Unity.Jobs;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Simulation.Spatial
{
    /// <summary>SPEC-23 AI-002: ordered hash results against independent double-precision reference geometry.</summary>
    public sealed class SpatialHashQueryTests
    {
        [BurstCompile(CompileSynchronously = true)]
        struct QueryJob : IJob
        {
            [ReadOnly] public SpatialHashQueryData Hash;
            public NativeArray<int> Result;
            public void Execute()
            {
                var output = new FixedList512Bytes<int>();
                Result[0] = Hash.QuerySegment(new float3(-128, 0, 0), new float3(128, 0, 0), 1, ref output);
                Result[1] = output.Length;
                for (int i = 0; i < output.Length; i++) Result[i + 2] = output[i];
            }
        }
        sealed class Fixture : IDisposable
        {
            public SpatialHash Hash = SpatialHash.Create(128);
            public Fixture(int count, bool dense = false)
            {
                var random = new Unity.Mathematics.Random(761231);
                for (int i = 0; i < count; i++) Hash.Proxies.Add(new SpatialProxy
                {
                    Entity = new Entity { Index = i + 1, Version = 2 },
                    Center = dense ? float3.zero : random.NextFloat3(new float3(-10000, -500, -10000), new float3(10000, 500, 10000)),
                    Radius = dense ? 1 : random.NextFloat(1, 500)
                });
                if (!dense) for (int i = 0; i < 10; i++) Hash.Proxies.Add(new SpatialProxy
                {
                    Entity = new Entity { Index = count + i + 1, Version = 2 },
                    Center = random.NextFloat3(-20000, 20000), Radius = random.NextFloat(513, 50000)
                });
                // Test-only independent bucket construction, not the future AI-004 builder.
                var cells = new SortedDictionary<long, List<int>>();
                for (int i = 0; i < Hash.Proxies.Length; i++)
                {
                    var p = Hash.Proxies[i];
                    if (p.Radius > 512) { Hash.Large.Add(i); continue; }
                    int2 lo = (int2)math.floor((p.Center.xz - p.Radius) / 128);
                    int2 hi = (int2)math.floor((p.Center.xz + p.Radius) / 128);
                    for (int x = lo.x; x <= hi.x; x++) for (int z = lo.y; z <= hi.y; z++)
                    {
                        long key = SpatialHash.CellKey(new int2(x, z));
                        if (!cells.TryGetValue(key, out var values)) { values = new List<int>(); cells.Add(key, values); }
                        values.Add(i);
                    }
                }
                Hash.Cells.Capacity = math.max(Hash.Cells.Capacity, cells.Count);
                foreach (var pair in cells)
                {
                    int start = Hash.EntryIndices.Length;
                    foreach (int index in pair.Value) Hash.EntryIndices.Add(index);
                    Hash.Cells.Add(pair.Key, new int2(start, pair.Value.Count));
                }
            }
            public void Dispose() => Hash.Dispose();
        }
        static bool Reference(in SpatialProxy p, float3 a, float3 b, float radius)
        {
            double3 start = a, delta = (double3)b - start, offset = (double3)p.Center - start;
            double lengthSq = math.dot(delta, delta);
            double t = lengthSq == 0 ? 0 : Math.Max(0, Math.Min(1, math.dot(offset, delta) / lengthSq));
            double3 distance = offset - delta * t;
            double reach = (double)p.Radius + radius;
            return math.dot(distance, distance) <= reach * reach;
        }
        static void Compare(Fixture f, float3 a, float3 b, float radius, bool segment)
        {
            var output = new FixedList512Bytes<int>(); output.Add(-42);
            int count = segment ? f.Hash.QuerySegment(a, b, radius, ref output) : f.Hash.QuerySphere(a, radius, ref output);
            int expected = 0;
            for (int i = 0; i < f.Hash.Proxies.Length; i++)
            {
                if (!Reference(f.Hash.Proxies[i], a, segment ? b : a, radius)) continue;
                if (expected < output.Capacity && (expected >= output.Length || output[expected] != i))
                    Assert.Fail($"Missing/unsorted match {i} at output {expected}, segment={segment}, a={a}, b={b}, radius={radius}");
                expected++;
            }
            if (count != expected || output.Length != Math.Min(expected, output.Capacity))
                Assert.Fail($"Count/output mismatch: expected {expected}, returned {count}/{output.Length}");
        }
        [Test] public void Segment_MatchesBruteForce()
        {
            using var f = new Fixture(10000); var r = new Unity.Mathematics.Random(66127);
            for (int i = 0; i < 10000; i++)
            {
                float3 a = r.NextFloat3(new float3(-11000, -800, -11000), new float3(11000, 800, 11000));
                float3 b = i % 31 == 0 ? a : a + r.NextFloat3(-1200, 1200);
                Compare(f, a, b, r.NextFloat(0, 100), true);
            }
        }
        [Test] public void Sphere_MatchesBruteForce()
        {
            using var f = new Fixture(10000); var r = new Unity.Mathematics.Random(55831);
            for (int i = 0; i < 10000; i++) Compare(f,
                r.NextFloat3(new float3(-11000, -800, -11000), new float3(11000, 800, 11000)), default, r.NextFloat(0, 1500), false);
        }
        [Test] public void Ascending_NoDuplicates()
        {
            using var f = new Fixture(80, true);
            Compare(f, new float3(-128, 0, 0), new float3(128, 0, 0), 1, true);
            Compare(f, float3.zero, default, 128, false);
            using var result = new NativeArray<int>(129, Allocator.TempJob);
            var handle = new QueryJob { Hash = f.Hash.AsReadOnly(), Result = result }.Schedule(f.Hash.BeginRead(default));
            f.Hash.EndRead(handle); f.Hash.CompleteAccess();
            Assert.AreEqual(80, result[0]); Assert.AreEqual(80, result[1]);
            for (int i = 0; i < 80; i++) Assert.AreEqual(i, result[i + 2], "Burst read-only query order");
            Assert.AreEqual(new int2(-1, -2), f.Hash.CellOf(new float3(-.1f, 0, -128.1f)));
            for (int i = 0; i < 80; i++) Assert.AreEqual(i, f.Hash.FindIndex(new Entity { Index = i + 1, Version = 2 }));
            Assert.AreEqual(-1, f.Hash.FindIndex(new Entity { Index = 40, Version = 1 }));
            Assert.AreEqual(-1, f.Hash.FindIndex(new Entity { Index = 81, Version = 2 }));
            Compare(f, new float3(10000), default, 1, false);
        }
        [Test] public void Overflow_CountsAll()
        {
            using var f = new Fixture(1500, true);
            Compare(f, float3.zero, default, 2, false); // raw-candidate scratch and output both overflow
            Compare(f, new float3(-1, 0, 0), new float3(1, 0, 0), 0, true);
            Compare(f, float3.zero, default, 1000000, false); // bounded cell traversal uses the same complete scan
        }
        [Test] public void LargeProxies_AlwaysConsidered()
        {
            using var f = new Fixture(100);
            foreach (int index in f.Hash.Large)
            {
                float3 center = f.Hash.Proxies[index].Center;
                Compare(f, center, default, 0, false);
                Compare(f, center, center, 0, true);
            }
        }
    }
}
