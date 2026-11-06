using System;
using System.Diagnostics;
using Game.Data.Blocks;
using Game.Tests.Common.Perf;
using Game.Voxel.Damage;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.Voxel.Damage
{
    [DisableAutoCreation] internal partial struct IntegritySelectionProbeSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnUpdate(ref SystemState state) { }
    }

    /// <summary>DMG-021: compare selection against the pre-optimization algorithm, including deferred seeds.</summary>
    public sealed class IntegritySelectionTests
    {
        [BurstCompile(CompileSynchronously = true)] struct ProductionJob : IJob
        {
            public SelectIntegrityGridsJob Selection;
            public void Execute() => Selection.Execute();
        }

        sealed class Fixture : IDisposable
        {
            readonly TestGridFactory factory = new TestGridFactory();
            readonly SystemHandle probe;
            public readonly NativeList<Entity> Roots;
            public readonly NativeList<CellRemoved> Removed;
            public readonly NativeList<GridRemovedRange> Ranges;
            readonly NativeList<Entity> candidates;
            readonly NativeList<IntegrityResult> results;
            readonly NativeReference<int> deferred;
            readonly Entity[] active = new Entity[4];
            readonly bool mixed;
            public Fixture(int count, bool mixed)
            {
                this.mixed = mixed;
                Roots = new NativeList<Entity>(count, Allocator.Persistent);
                Removed = new NativeList<CellRemoved>(Allocator.Persistent);
                Ranges = new NativeList<GridRemovedRange>(Allocator.Persistent);
                candidates = new NativeList<Entity>(count, Allocator.Persistent);
                results = new NativeList<IntegrityResult>(count, Allocator.Persistent);
                deferred = new NativeReference<int>(Allocator.Persistent);
                for (int i = 0; i < count; i++)
                {
                    Entity root;
                    if (mixed && i < active.Length)
                    {
                        var source = factory.Box(new int3(-1), new int3(1));
                        source.SetCell(int3.zero, default);
                        root = factory.Spawn(source); active[i] = root;
                    }
                    else
                    {
                        root = factory.EM.CreateEntity(typeof(VoxelGrid), typeof(GridIntegrityState));
                        factory.EM.AddBuffer<IntegritySeed>(root);
                    }
                    factory.EM.SetComponentData(root, new GridIntegrityState { ConsecutiveDeferrals = (byte)(i % 5) });
                    Roots.Add(root);
                }
                // Input entity-query order need not be canonical.
                for (int i = 0; i < Roots.Length / 2; i++)
                { var e = Roots[i]; Roots[i] = Roots[Roots.Length - i - 1]; Roots[Roots.Length - i - 1] = e; }
                if (mixed)
                {
                    Removed.Add(new CellRemoved { Grid = active[0], Cell = int3.zero, Reason = CellRemovedReason.Destroyed });
                    Removed.Add(new CellRemoved { Grid = active[2], Cell = int3.zero, Reason = CellRemovedReason.DetachedBody });
                    Ranges.Add(new GridRemovedRange { Grid = active[0], Count = 1 });
                    Ranges.Add(new GridRemovedRange { Grid = active[2], Start = 1, Count = 1 });
                    Ranges.Add(new GridRemovedRange { Grid = active[3], Start = 2, Count = 0 });
                }
                probe = factory.World.CreateSystem<IntegritySelectionProbeSystem>();
                Reset();
            }
            public void Reset()
            {
                if (!mixed) return;
                foreach (var e in active) factory.EM.GetBuffer<IntegritySeed>(e).Clear();
                var seeds = factory.EM.GetBuffer<IntegritySeed>(active[1]);
                seeds.Add(new IntegritySeed { Cell = new int3(1, 0, 0) });
                seeds.Add(new IntegritySeed { Cell = new int3(1, 0, 0) });
                seeds.Add(new IntegritySeed { Cell = new int3(100) }); // stale retained seed
                factory.EM.GetBuffer<IntegritySeed>(active[3]).Add(new IntegritySeed { Cell = new int3(-1, 0, 0) });
            }
            public SelectIntegrityGridsJob Job(int limit)
            {
                ref var state = ref factory.World.Unmanaged.ResolveSystemStateRef(probe);
                return new SelectIntegrityGridsJob
                {
                    Roots = Roots, Removed = Removed, Ranges = Ranges, Reader = GridCellReader.Create(ref state),
                    Seeds = state.GetBufferLookup<IntegritySeed>(), Candidates = candidates, Results = results,
                    Deferred = deferred, Limit = limit
                };
            }
            public void Run(bool legacy, int limit)
            {
                var job = Job(limit);
                if (legacy) Legacy(job).Run(); else new ProductionJob { Selection = job }.Run();
            }
            public double Sample(bool legacy)
            {
                Reset();
                var job = Job(2); var before = Legacy(job); var after = new ProductionJob { Selection = job };
                long start = Stopwatch.GetTimestamp();
                if (legacy) before.Run(); else after.Run();
                return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            public Snapshot Capture()
            {
                var snapshot = new Snapshot { Candidates = candidates.AsArray().ToArray(), Results = results.AsArray().ToArray(), Deferred = deferred.Value,
                    Seeds = new int3[Roots.Length][], Deferrals = new byte[Roots.Length] };
                for (int i = 0; i < Roots.Length; i++)
                {
                    var seeds = factory.EM.GetBuffer<IntegritySeed>(Roots[i]);
                    snapshot.Seeds[i] = new int3[seeds.Length];
                    for (int j = 0; j < seeds.Length; j++) snapshot.Seeds[i][j] = seeds[j].Cell;
                    snapshot.Deferrals[i] = factory.EM.GetComponentData<GridIntegrityState>(Roots[i]).ConsecutiveDeferrals;
                }
                return snapshot;
            }
            public void Dispose()
            {
                deferred.Dispose(); results.Dispose(); candidates.Dispose(); Ranges.Dispose(); Removed.Dispose(); Roots.Dispose(); factory.Dispose();
            }
        }
        sealed class Snapshot
        {
            public Entity[] Candidates; public IntegrityResult[] Results; public int Deferred;
            public int3[][] Seeds; public byte[] Deferrals;
        }
        static LegacyIntegritySelectionJob Legacy(SelectIntegrityGridsJob job) => new LegacyIntegritySelectionJob
        {
            Roots = job.Roots, Removed = job.Removed, Ranges = job.Ranges, Reader = job.Reader, Seeds = job.Seeds,
            Candidates = job.Candidates, Results = job.Results, Deferred = job.Deferred, Limit = job.Limit
        };

        [Test] public void Selection_MatchesLegacy_WithIdleDirtyAndRetainedSeeds()
        {
            using var f = new Fixture(92, true);
            for (int phase = 0; phase < 2; phase++)
            {
                if (phase == 1) f.Ranges.Clear(); // previous ticks' seeds must survive without new removals
                foreach (int limit in new[] { 0, 1, 2, 92 })
                {
                    f.Reset(); var initial = f.Capture(); f.Run(true, limit); var expected = f.Capture();
                    f.Reset(); f.Run(false, limit); var actual = f.Capture();
                    CollectionAssert.AreEqual(expected.Candidates, actual.Candidates);
                    CollectionAssert.AreEqual(expected.Results, actual.Results);
                    Assert.AreEqual(expected.Deferred, actual.Deferred);
                    CollectionAssert.AreEqual(initial.Deferrals, actual.Deferrals, "Selection must not change race deferral state");
                    for (int i = 0; i < expected.Seeds.Length; i++) CollectionAssert.AreEqual(expected.Seeds[i], actual.Seeds[i], $"root {i}, phase {phase}, limit {limit}");
                    Assert.AreEqual(phase == 0 ? 3 : 2, actual.Candidates.Length + actual.Deferred, "Dirty and retained-seed roots remain eligible");
                }
            }
        }

        [TestCase(92, false), TestCase(1000, false), TestCase(92, true), TestCase(1000, true)]
        [Performance, Category("Performance")]
        public void Selection_PairedBaseline(int grids, bool mixed)
        {
            using var f = new Fixture(grids, mixed);
            var baseline = new double[300]; var optimized = new double[300];
            for (int i = 0; i < 360; i++)
            {
                // Alternate order to balance drift; fixture reset and construction are outside the measured interval.
                double a, b;
                if ((i & 1) == 0) { a = f.Sample(true); b = f.Sample(false); }
                else { b = f.Sample(false); a = f.Sample(true); }
                if (i >= 60) { baseline[i - 60] = a; optimized[i - 60] = b; }
            }
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("LegacySelection", baseline).AddSamples("ProductionSelection", optimized);
            var old = run.Get("LegacySelection"); var current = run.Get("ProductionSelection");
            Assert.Greater(old.Median, 0); Assert.Greater(current.Median, 0);
            run.Write();
            TestContext.WriteLine($"{grids} grids, mixed={mixed}: baseline median/p95 {old.Median:F6}/{old.P95:F6} ms; production {current.Median:F6}/{current.P95:F6} ms; median speedup {old.Median / current.Median:F3}x. Includes Burst Run overhead, excludes fixture reset; subsystem comparison, not whole-frame speedup.");
        }
    }

    // Frozen pre-DMG-021 production selection for differential correctness and paired timing.
    [BurstCompile(CompileSynchronously = true)] internal struct LegacyIntegritySelectionJob : IJob
    {
        [ReadOnly] public NativeList<Entity> Roots;
        [ReadOnly] public NativeList<CellRemoved> Removed;
        [ReadOnly] public NativeList<GridRemovedRange> Ranges;
        public GridCellReader Reader;
        public BufferLookup<IntegritySeed> Seeds;
        public NativeList<Entity> Candidates;
        public NativeList<IntegrityResult> Results;
        public NativeReference<int> Deferred;
        public int Limit;
        public void Execute()
        {
            Candidates.Clear(); Results.Clear(); Deferred.Value = 0;
            using var ordered = new NativeList<Entity>(Allocator.Temp);
            foreach (var root in Roots) ordered.Add(root);
            ordered.Sort(new EntityOrder());
            foreach (var root in ordered)
            {
                using var positions = new NativeList<int3>(Allocator.Temp);
                using var seen = new NativeHashSet<int3>(16, Allocator.Temp);
                foreach (var range in Ranges)
                    if (range.Grid == root)
                        for (int i = range.Start; i < range.Start + range.Count; i++)
                        {
                            var removed = Removed[i]; if (removed.Reason != CellRemovedReason.Destroyed) continue;
                            for (int face = 0; face < 6; face++) Add(root, removed.Cell + BrickMath.FaceDirection((BlockFace)face), positions, seen);
                        }
                var seeds = Seeds[root];
                foreach (var seed in seeds) Add(root, seed.Cell, positions, seen);
                seeds.Clear(); foreach (var p in positions) seeds.Add(new IntegritySeed { Cell = p });
                if (positions.Length == 0) continue;
                if (Candidates.Length < math.max(0, Limit)) { Candidates.Add(root); Results.Add(default); }
                else Deferred.Value++;
            }
        }
        void Add(Entity root, int3 p, NativeList<int3> positions, NativeHashSet<int3> seen)
        { if (Reader.IsSolid(root, p) && seen.Add(p)) positions.Add(p); }
        struct EntityOrder : System.Collections.Generic.IComparer<Entity>
        { public int Compare(Entity a, Entity b) => a.Index.CompareTo(b.Index); }
    }
}
