using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Generation;
using Game.Galaxy.Travel;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Galaxy.Travel
{
    public sealed class RoutePlannerTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        readonly struct Edge
        {
            internal readonly int A, B; internal readonly float Length;
            internal readonly bool Hidden; internal readonly ushort Gate;
            internal Edge(int a, int b, float length, bool hidden = false, ushort gate = 0)
            { A = Math.Min(a, b); B = Math.Max(a, b); Length = length; Hidden = hidden; Gate = gate; }
        }
        static GalaxyDef Graph(int count, params Edge[] edges)
        {
            Array.Sort(edges, (a, b) => a.A != b.A ? a.A.CompareTo(b.A) : a.B.CompareTo(b.B));
            var g = new GalaxyDef(1, 1, Array.Empty<StarSystemTemplateDefinition>(), Array.Empty<AuthoredStarSystemDefinition>(),
                Array.Empty<BodyTemplateDefinition>(), StarSystemId.FromProceduralIndex(0));
            try
            {
                g.StarSystems = new NativeArray<StarSystemDef>(count, Allocator.Persistent);
                g.Lanes = new NativeArray<LaneDef>(edges.Length, Allocator.Persistent);
                g.Adjacency = new NativeArray<int>(edges.Length * 2, Allocator.Persistent);
                g.IndexById = new NativeHashMap<uint, int>(count, Allocator.Persistent);
                var neighbours = new List<int>[count];
                for (int i = 0; i < count; i++) neighbours[i] = new List<int>();
                var gates = (List<LaneGateSpec>)g.LaneGates;
                for (int i = 0; i < edges.Length; i++)
                {
                    var edge = edges[i];
                    g.Lanes[i] = new LaneDef { A = StarSystemId.FromProceduralIndex(edge.A), B = StarSystemId.FromProceduralIndex(edge.B),
                        LengthLy = edge.Length, Flags = edge.Hidden ? LaneFlags.HiddenUntilKnown : LaneFlags.None, GateIndex = edge.Gate };
                    neighbours[edge.A].Add(i); neighbours[edge.B].Add(i);
                    while (gates.Count < edge.Gate) gates.Add(new LaneGateSpec());
                }
                int offset = 0;
                for (int i = 0; i < count; i++)
                {
                    var id = StarSystemId.FromProceduralIndex(i);
                    neighbours[i].Sort((a, b) => GalaxyDef.Neighbour(g.Lanes[a], id).CompareTo(GalaxyDef.Neighbour(g.Lanes[b], id)));
                    g.IndexById.Add(id.Value, i);
                    g.StarSystems[i] = new StarSystemDef { Id = id, FirstAdjacency = offset, LaneCount = (ushort)neighbours[i].Count };
                    foreach (int lane in neighbours[i]) g.Adjacency[offset++] = lane;
                }
                return g;
            }
            catch { g.Dispose(); throw; }
        }
        static void Known(GalaxyDef g, RouteScratch scratch)
        {
            scratch.EnsureCapacity(g.StarSystems.Length, g.LaneGates.Count, g.Lanes.Length);
            for (int i = 0; i < g.StarSystems.Length; i++) scratch.SystemFlags[i] = 1;
        }
        static void Populate(GalaxyDef g, RouteScratch scratch, GalaxyState state)
        {
            scratch.EnsureCapacity(g.StarSystems.Length, g.LaneGates.Count, g.Lanes.Length);
            for (int i = 0; i < scratch.SystemFlags.Length; i++) scratch.SystemFlags[i] = 0;
            foreach (uint id in state.Known) if (g.TryGetIndex(new StarSystemId(id), out int index)) scratch.SystemFlags[index] |= 1;
            foreach (uint id in state.Visited) if (g.TryGetIndex(new StarSystemId(id), out int index)) scratch.SystemFlags[index] |= 2;
        }
        static int Lane(GalaxyDef g, int a, int b)
        {
            Assert.IsTrue(g.TryFindLane(g.StarSystems[a].Id, g.StarSystems[b].Id, out int lane)); return lane;
        }
        static double BruteForceDistance(GalaxyDef g, RouteScratch scratch, int source, int target)
        {
            // Enumerate walk costs by hop count up to N-1, without a heap, settlement or predecessor tie-break.
            int count = g.StarSystems.Length; var costs = new double[count]; var next = new double[count];
            for (int i = 0; i < count; i++) costs[i] = double.PositiveInfinity;
            if ((scratch.SystemFlags[source] & 1) == 0 || (scratch.SystemFlags[target] & 1) == 0) return double.PositiveInfinity;
            costs[source] = 0;
            for (int hop = 1; hop < count; hop++)
            {
                Array.Copy(costs, next, count); bool changed = false;
                foreach (var lane in g.Lanes)
                {
                    g.TryGetIndex(lane.A, out int a); g.TryGetIndex(lane.B, out int b);
                    byte fa = scratch.SystemFlags[a], fb = scratch.SystemFlags[b];
                    if ((fa & 1) == 0 || (fb & 1) == 0) continue;
                    if ((lane.Flags & LaneFlags.HiddenUntilKnown) != 0 && ((fa & 2) == 0 || (fb & 2) == 0)) continue;
                    if (lane.GateIndex != 0 && scratch.GateOpen[lane.GateIndex - 1] == 0) continue;
                    double toB = costs[a] + lane.LengthLy, toA = costs[b] + lane.LengthLy;
                    if (toB < next[b]) { next[b] = toB; changed = true; }
                    if (toA < next[a]) { next[a] = toA; changed = true; }
                }
                var swap = costs; costs = next; next = swap;
                if (!changed) break;
            }
            return costs[target];
        }

        [Test] public void MatchesBruteForce()
        {
            using var fixture = TestGalaxyFixtures.Small;
            fixture.Generation.AuthoredLanes[0].hiddenUntilKnown = true;
            var catalog = ContentCatalog.Build(fixture.Definitions, new ValidationReport("Route reference"));
            using var state = new GalaxyState(); using var scratch = new RouteScratch(); var path = new List<int>(64);
            for (uint seed = 1; seed <= 200; seed++)
            {
                using var g = GalaxyDefBuilder.Build(catalog, 0xA11CEUL + seed, GalaxyConstants.CurrentGeneratorVersion);
                var rng = RngStreams.For(0xA11CEUL + seed, RngPurpose.GalaxyLanes, 1); state.Clear();
                for (int i = 0; i < g.StarSystems.Length; i++)
                    if (rng.NextBool()) { state.AddKnown(g.StarSystems[i].Id); if (rng.NextBool()) state.AddVisited(g.StarSystems[i].Id); }
                int source = rng.NextInt(g.StarSystems.Length), target = rng.NextInt(g.StarSystems.Length);
                state.AddKnown(g.StarSystems[source].Id); state.AddKnown(g.StarSystems[target].Id); Populate(g, scratch, state);
                double expected = BruteForceDistance(g, scratch, source, target);
                bool found = RoutePlanner.TryFindRoute(g, scratch, source, target, false, path, out double total);
                Assert.AreEqual(!double.IsPositiveInfinity(expected), found, "Seed " + seed);
                if (!found) { Assert.IsEmpty(path); Assert.AreEqual(0, total); continue; }
                Assert.That(total, Is.EqualTo(expected).Within(1e-9), "Seed " + seed);
                var current = g.StarSystems[source].Id; double sum = 0;
                foreach (int index in path)
                {
                    var lane = g.Lanes[index]; Assert.IsTrue(lane.A.Equals(current) || lane.B.Equals(current));
                    current = GalaxyDef.Neighbour(lane, current); sum += lane.LengthLy;
                    Assert.IsTrue(state.IsKnown(current));
                }
                Assert.AreEqual(g.StarSystems[target].Id, current); Assert.AreEqual(total, sum);
            }
        }

        [Test] public void TieBreak_Deterministic()
        {
            using var hops = Graph(5, new Edge(0, 4, 2), new Edge(0, 1, 1), new Edge(1, 4, 1));
            using var parent = Graph(5, new Edge(0, 1, 1.5f), new Edge(1, 4, .5f), new Edge(0, 2, .5f), new Edge(2, 4, 1.5f));
            using var scratch = new RouteScratch(); var path = new List<int>();
            for (int run = 0; run < 10; run++)
            {
                Known(hops, scratch); Assert.IsTrue(RoutePlanner.TryFindRoute(hops, scratch, 0, 4, false, path, out double total));
                CollectionAssert.AreEqual(new[] { Lane(hops, 0, 4) }, path); Assert.AreEqual(2, total);
                Known(parent, scratch); Assert.IsTrue(RoutePlanner.TryFindRoute(parent, scratch, 0, 4, false, path, out total));
                CollectionAssert.AreEqual(new[] { Lane(parent, 0, 1), Lane(parent, 1, 4) }, path); Assert.AreEqual(2, total);
            }
        }

        [Test] public void Visibility_Respected()
        {
            using var g = Graph(4, new Edge(0, 1, 1), new Edge(1, 3, 1), new Edge(0, 2, 1, true), new Edge(2, 3, .5f));
            using var scratch = new RouteScratch(); Known(g, scratch); var path = new List<int>();
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out double total)); Assert.AreEqual(2, total);
            CollectionAssert.AreEqual(new[] { Lane(g, 0, 1), Lane(g, 1, 3) }, path);
            scratch.SystemFlags[1] = 0;
            Assert.IsFalse(RoutePlanner.TryFindRoute(g, scratch, 0, 3, true, path, out total)); Assert.IsEmpty(path); Assert.AreEqual(0, total);
            scratch.SystemFlags[0] = 3; scratch.SystemFlags[2] = 3;
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out total)); Assert.AreEqual(1.5, total);
            CollectionAssert.AreEqual(new[] { Lane(g, 0, 2), Lane(g, 2, 3) }, path);
            scratch.SystemFlags[2] = 2;
            Assert.IsFalse(RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out total), "Visited-only malformed input cannot expose an unknown vertex.");
            Known(g, scratch); scratch.SystemFlags[3] = 0;
            Assert.IsFalse(RoutePlanner.TryFindRoute(g, scratch, 0, 3, true, path, out total)); Assert.IsEmpty(path);
            scratch.SystemFlags[0] = 0;
            Assert.IsFalse(RoutePlanner.TryFindRoute(g, scratch, 0, 0, true, path, out total));
            scratch.SystemFlags[0] = 1;
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 0, false, path, out total)); Assert.IsEmpty(path); Assert.AreEqual(0, total);
            Assert.IsFalse(RoutePlanner.TryFindRoute(g, scratch, -1, 3, false, path, out total));
            Assert.IsFalse(RoutePlanner.TryFindRoute(g, scratch, 0, 4, false, path, out total)); Assert.IsEmpty(path); Assert.AreEqual(0, total);
        }

        [Test] public void Locks_Respected()
        {
            using var g = Graph(4, new Edge(0, 1, 1, gate: 1), new Edge(1, 3, 1), new Edge(0, 2, 2), new Edge(2, 3, 2, gate: 2));
            using var scratch = new RouteScratch(); Known(g, scratch); scratch.GateOpen[1] = 1; var path = new List<int>();
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out double total)); Assert.AreEqual(4, total);
            CollectionAssert.AreEqual(new[] { Lane(g, 0, 2), Lane(g, 2, 3) }, path);
            scratch.GateOpen[0] = 1;
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out total)); Assert.AreEqual(2, total);
            scratch.GateOpen[0] = 0; scratch.GateOpen[1] = 0;
            Assert.IsFalse(RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out total)); Assert.IsEmpty(path); Assert.AreEqual(0, total);
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 3, true, path, out total)); Assert.AreEqual(2, total);
            scratch.SystemFlags[1] = 0;
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 3, true, path, out total)); Assert.AreEqual(4, total);
        }

        static unsafe ulong[] Addresses(RouteScratch scratch) => new[]
        {
            (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.SystemFlags), (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.GateOpen),
            (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.Dist), (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.Hops),
            (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.PrevLane), (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.PrevSystem),
            (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.Settled), (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.Heap.AsArray()),
            (ulong)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch.PathLanes.AsArray())
        };
        [Test] public void Scratch_ReusedAndDisposed()
        {
            using var g = Graph(4, new Edge(0, 1, 1, gate: 1), new Edge(1, 3, 1));
            using var scratch = new RouteScratch(); Known(g, scratch); scratch.GateOpen[0] = 1; var path = new List<int>(4);
            Assert.IsTrue(RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out _));
            var addresses = Addresses(scratch); int heapCapacity = scratch.Heap.Capacity, pathCapacity = scratch.PathLanes.Capacity;
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool found = RoutePlanner.TryFindRoute(g, scratch, 0, 3, false, path, out double total);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(found); Assert.AreEqual(2, total); Assert.AreEqual(0, allocated);
            CollectionAssert.AreEqual(addresses, Addresses(scratch));
            Assert.AreEqual(heapCapacity, scratch.Heap.Capacity); Assert.AreEqual(pathCapacity, scratch.PathLanes.Capacity);
            scratch.EnsureCapacity(8, 3, 12); Assert.AreEqual(1, scratch.SystemFlags[0]); Assert.AreEqual(1, scratch.GateOpen[0]);
            Assert.That(scratch.SystemFlags.Length, Is.GreaterThanOrEqualTo(8)); Assert.That(scratch.GateOpen.Length, Is.GreaterThanOrEqualTo(3));
            var grown = Addresses(scratch); scratch.EnsureCapacity(4, 1, 2); CollectionAssert.AreEqual(grown, Addresses(scratch));
            scratch.Dispose(); scratch.Dispose();
            Assert.IsFalse(scratch.SystemFlags.IsCreated || scratch.GateOpen.IsCreated || scratch.Dist.IsCreated || scratch.Hops.IsCreated ||
                scratch.PrevLane.IsCreated || scratch.PrevSystem.IsCreated || scratch.Settled.IsCreated || scratch.Heap.IsCreated ||
                scratch.PathLanes.IsCreated || scratch.TotalLy.IsCreated || scratch.Found.IsCreated);
            Assert.Throws<ObjectDisposedException>(() => scratch.EnsureCapacity(1, 0, 0));
        }

        [Test, Performance, Category("Performance")]
        public void Perf_1000()
        {
            var edges = new List<Edge>(2500);
            for (int stride = 1; edges.Count < 2500; stride++)
                for (int a = 0; a + stride < 1000 && edges.Count < 2500; a++) edges.Add(new Edge(a, a + stride, stride));
            using var g = Graph(1000, edges.ToArray()); using var scratch = new RouteScratch(); Known(g, scratch);
            var path = new List<int>(1000);
            for (int i = 0; i < 60; i++) RoutePlanner.TryFindRoute(g, scratch, 0, 999, false, path, out _);
            Stopwatch.GetTimestamp(); var samples = new double[300]; bool found = false; double total = 0;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < samples.Length; i++)
            {
                long start = Stopwatch.GetTimestamp(); found = RoutePlanner.TryFindRoute(g, scratch, 0, 999, false, path, out total);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Assert.IsTrue(found); Assert.AreEqual(999, total); Assert.AreEqual(0, allocated);
            Assert.AreEqual(1000, g.StarSystems.Length); Assert.AreEqual(2500, g.Lanes.Length);
            PerfRun.Gate("RoutePlanner1000", samples, .5);
        }
    }
}
