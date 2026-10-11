using System.Collections.Generic;
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
    /// <summary>PHY-007 record/pool integrity, ordered handover, discarded partial batches and all-owner teardown.</summary>
    public sealed class ColliderPipelineDataTests
    {
        static BlobAssetReference<Collider> Box() => BoxCollider.Create(new BoxGeometry
        { Center = float3.zero, Size = new float3(1), Orientation = quaternion.identity, BevelRadius = 0 }, PhysicsLayers.Grid, PhysicsLayers.GridMaterial);
        static BrickMaskBits Mask() { var mask = new BrickMaskBits(); mask.Set(0, true); return mask; }
        static int Record(ref ColliderBuildCache cache, uint serial, bool installed = false) => ColliderPipeline.AddRecord(ref cache.C,
            new Entity { Index = (int)serial, Version = 1 }, serial, 0.5f, GridMotion.Dynamic, installed: installed ? Box() : default);
        static int Update(ref ColliderBuildCache cache, uint serial, uint tick, params int3[] coords)
        {
            int update = ColliderPipeline.GetOrCreateUpdate(ref cache.C, serial, tick, tick - 7, ColliderPriority.Damage);
            var mask = Mask();
            foreach (var coord in coords) ColliderPipeline.SetSnapshot(ref cache.C, update, coord, mask, false);
            return update;
        }
        static void ScheduleBricks(ref ColliderBuildCache cache, ref ColliderBatchQueue batch, int update, int count)
        {
            var u = cache.C.Updates[update];
            if (u.State == UpdateState.Collecting) ColliderPipeline.Seal(ref cache.C, update);
            for (int i = 0; i < count; i++)
            {
                u = cache.C.Updates[update]; var brick = u.Bricks[u.BricksScheduled];
                var mask = ColliderPipeline.TakeNextMask(ref cache.C, update, out bool removed);
                batch.B.Bricks.Add(new BrickBuildItem { Update = update, Serial = u.Serial, Coord = brick.Coord, CellSize = 0.5f, Removed = removed ? (byte)1 : (byte)0 });
                batch.B.Masks.Add(mask);
                batch.B.Results.Add(new BrickBuildResult { Blob = removed ? default : Box(), BoxCount = removed ? 0 : 1, Removed = removed ? (byte)1 : (byte)0 });
            }
        }
        static void Assembly(ref ColliderBuildCache cache, ref ColliderBatchQueue batch, int update, int children)
        {
            batch.B.Assemblies.Add(new AssemblyItem { Update = update, Serial = cache.C.Updates[update].Serial, CellSize = 0.5f });
            batch.B.AssemblyResults.Add(new AssemblyResult { Compound = children == 0 ? default : Box(), ChildCount = children, BoxCount = children });
        }
        [BurstCompile(CompileSynchronously = true)]
        struct FinalizeJob : IJob
        {
            public ColliderCacheContainers Cache;
            public ColliderBatchContainers Batch;
            public void Execute() => ColliderPipeline.Finalize(ref Cache, ref Batch);
        }
        static void Finalize(ref ColliderBuildCache cache, ref ColliderBatchQueue batch)
        {
            ColliderPipelineInvariants.Check(cache, batch);
            var handle = new FinalizeJob { Cache = cache.C, Batch = batch.B }.Schedule(cache.Acquire(default));
            cache.Release(handle); cache.CompleteAccess();
            batch.State.Value = default;
            ColliderPipelineInvariants.Check(cache, batch);
        }
        static void DisposeRetired(ref ColliderBuildCache cache)
        {
            // No physics world in these tests; stand in for the completed deferred-disposal interval.
            for (int i = 0; i < cache.C.Retired.Length; i++) cache.C.Retired[i].Dispose();
            ColliderPipeline.ClearTransferredRetired(ref cache.C);
        }

        [Test]
        public void Records_RandomOps_IndexConsistent()
        {
            var cache = ColliderBuildCache.Create(); var batch = ColliderBatchQueue.Create();
            try
            {
                var random = new Random(0xDEC02Du); var expected = new HashSet<uint>(); uint next = 1;
                for (int op = 0; op < 10000; op++)
                {
                    if (expected.Count == 0 || (expected.Count < 128 && random.NextBool())) { Record(ref cache, next); expected.Add(next++); }
                    else
                    {
                        int index = random.NextInt(cache.C.Grids.Length); expected.Remove(cache.C.Grids[index].Serial);
                        ColliderPipeline.RemoveRecord(ref cache.C, index, batch.B);
                    }
                    Assert.AreEqual(expected.Count, cache.C.GridIndex.Count);
                    foreach (uint serial in expected)
                    { Assert.IsTrue(cache.C.GridIndex.TryGetValue(serial, out int index)); Assert.AreEqual(serial, cache.C.Grids[index].Serial); }
                    ColliderPipelineInvariants.Check(cache, batch);
                }
            }
            finally { batch.Dispose(); cache.Dispose(); }
        }

        [Test]
        public void Finalize_AppliesInOrder()
        {
            var cache = ColliderBuildCache.Create(); var batch = ColliderBatchQueue.Create();
            try
            {
                Record(ref cache, 1, true);
                int initial = Update(ref cache, 1, 8, new int3(0), new int3(1, 0, 0), new int3(2, 0, 0));
                ScheduleBricks(ref cache, ref batch, initial, 3); Assembly(ref cache, ref batch, initial, 3); Finalize(ref cache, ref batch);
                ColliderPipeline.FreeUpdate(ref cache.C, initial); DisposeRetired(ref cache);
                var unchanged = cache.C.Grids[0].Children[1].Blob;
                int first = Update(ref cache, 1, 12, new int3(2, 0, 0));
                ScheduleBricks(ref cache, ref batch, first, 1); Assembly(ref cache, ref batch, first, 3);
                int second = Update(ref cache, 1, 16, new int3(2, 0, 0), new int3(0), new int3(-1, 0, 0));
                ColliderPipeline.SetSnapshot(ref cache.C, second, new int3(0), default, true);
                ScheduleBricks(ref cache, ref batch, second, 3); Assembly(ref cache, ref batch, second, 3);
                var replacement = batch.B.Results[1].Blob; var inserted = batch.B.Results[3].Blob;
                var firstCompound = batch.B.AssemblyResults[0].Compound; var secondCompound = batch.B.AssemblyResults[1].Compound;
                // Borrowed snapshots can alias cache children without becoming a second owner.
                batch.B.AssemblyChildren.Add(cache.C.Grids[0].Children[1]);
                Finalize(ref cache, ref batch);
                var children = cache.C.Grids[0].Children;
                Assert.AreEqual(3, children.Length);
                Assert.AreEqual(new int3(-1, 0, 0), children[0].Coord); Assert.AreEqual(inserted, children[0].Blob);
                Assert.AreEqual(unchanged, children[1].Blob); Assert.AreEqual(replacement, children[2].Blob);
                Assert.AreEqual(3, cache.C.Retired.Length); // original x2, first update's x2, removed x0
                Assert.AreEqual(firstCompound, cache.C.Updates[first].Compound); Assert.AreEqual(secondCompound, cache.C.Updates[second].Compound);
                Assert.AreEqual(UpdateState.Built, cache.C.Updates[first].State); Assert.AreEqual(UpdateState.Built, cache.C.Updates[second].State);
                Assert.AreEqual(3, cache.C.Updates[second].ChildCount); Assert.AreEqual(3, cache.C.Updates[second].BoxCount);
                Assert.AreEqual(0, batch.B.Bricks.Length); Assert.AreEqual(0, batch.B.AssemblyChildren.Length);
                Assert.AreEqual(3, cache.C.Stats.Value.CachedBoxes); Assert.AreEqual(2, cache.C.Stats.Value.PendingUpdates);
            }
            finally { batch.Dispose(); cache.Dispose(); }
        }

        [Test]
        public void Finalize_DiscardedUpdate_RetiresResults()
        {
            var cache = ColliderBuildCache.Create(); var batch = ColliderBatchQueue.Create();
            try
            {
                Record(ref cache, 1, true); Record(ref cache, 2, true);
                int partial = Update(ref cache, 1, 8, new int3(0), new int3(1, 0, 0));
                int whole = Update(ref cache, 2, 8, new int3(0));
                ScheduleBricks(ref cache, ref batch, partial, 1);
                ScheduleBricks(ref cache, ref batch, whole, 1); Assembly(ref cache, ref batch, whole, 1);
                ColliderPipeline.RemoveRecord(ref cache.C, cache.C.GridIndex[1], batch.B);
                ColliderPipeline.RemoveRecord(ref cache.C, cache.C.GridIndex[2], batch.B);
                Assert.AreEqual(UpdateState.Discarded, cache.C.Updates[partial].State);
                Assert.AreEqual(0, cache.C.FreeUpdates.Length, "Do not recycle an in-flight partial update");
                Finalize(ref cache, ref batch);
                Assert.AreEqual(5, cache.C.Retired.Length); Assert.AreEqual(2, cache.C.FreeUpdates.Length);
                Assert.AreEqual(cache.C.Masks.Length, cache.C.FreeMasks.Length);
                Assert.AreEqual(0, cache.C.Stats.Value.PendingUpdates); Assert.AreEqual(0, cache.C.Stats.Value.PendingBricks);
                Assert.AreEqual(0, cache.C.GridIndex.Count);
            }
            finally { batch.Dispose(); cache.Dispose(); }
        }

        [BurstCompile(CompileSynchronously = true)]
        struct PendingBuildJob : IJob
        {
            public NativeList<BrickBuildResult> Bricks;
            public NativeList<AssemblyResult> Assemblies;
            public void Execute()
            {
                Bricks[0] = new BrickBuildResult { Blob = Box(), BoxCount = 1 };
                Assemblies[0] = new AssemblyResult { Compound = Box(), ChildCount = 1, BoxCount = 1 };
            }
        }
        static void AllOwners()
        {
            var queue = ColliderDirtyQueue.Create(); var cache = ColliderBuildCache.Create(); var batch = ColliderBatchQueue.Create();
            try
            {
                Record(ref cache, 1, true);
                int built = Update(ref cache, 1, 8, new int3(0));
                ScheduleBricks(ref cache, ref batch, built, 1); Assembly(ref cache, ref batch, built, 1); Finalize(ref cache, ref batch);
                ColliderPipeline.Retire(ref cache.C, Box());
                int pending = Update(ref cache, 1, 12, new int3(1, 0, 0));
                ColliderPipeline.Seal(ref cache.C, pending);
                batch.B.Masks.Add(ColliderPipeline.TakeNextMask(ref cache.C, pending, out _));
                batch.B.Bricks.Add(new BrickBuildItem { Update = pending, Serial = 1, Coord = new int3(1, 0, 0) });
                batch.B.Results.Add(default);
                batch.B.Assemblies.Add(new AssemblyItem { Update = pending, Serial = 1 }); batch.B.AssemblyResults.Add(default);
                batch.B.AssemblyChildren.Add(cache.C.Grids[0].Children[0]);
                batch.State.Value = new ColliderBatchState { InFlight = 1, Handle = new PendingBuildJob { Bricks = batch.B.Results, Assemblies = batch.B.AssemblyResults }.Schedule() };
            }
            finally { batch.Dispose(); cache.Dispose(); queue.Dispose(); }
        }
        [Test]
        public void Dispose_AllOwners_NoLeak()
        {
            var before = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try { AllOwners(); UnsafeUtility.CheckForLeaks(); AllOwners(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks()); }
            finally { NativeLeakDetection.Mode = before; }
        }

        [Test]
        public void Invariants_HoldAfterRandomPipelineOps()
        {
            var cache = ColliderBuildCache.Create(); var batch = ColliderBatchQueue.Create();
            try
            {
                var random = new Random(0xB1BE11u); var expected = new Dictionary<uint, HashSet<int3>>();
                for (int op = 0; op < 1000; op++)
                {
                    uint serial = random.NextUInt(1, 9);
                    if (!expected.ContainsKey(serial)) { Record(ref cache, serial); expected.Add(serial, new HashSet<int3>()); }
                    int update = ColliderPipeline.GetOrCreateUpdate(ref cache.C, serial, (uint)(op + 8), (uint)op, ColliderPriority.Spawn);
                    var edits = new Dictionary<int3, bool>(); var mask = Mask();
                    for (int i = 0, count = random.NextInt(1, 12); i < count; i++)
                    {
                        int3 coord = new int3(random.NextInt(-3, 4), 0, 0); bool removed = random.NextBool(); edits[coord] = removed;
                        ColliderPipeline.SetSnapshot(ref cache.C, update, coord, mask, removed);
                    }
                    ColliderPipelineInvariants.Check(cache, batch);
                    int total = cache.C.Updates[update].Bricks.Length;
                    int first = random.NextInt(1, total + 1);
                    ScheduleBricks(ref cache, ref batch, update, first);
                    bool discard = random.NextInt(5) == 0;
                    if (discard)
                    {
                        ColliderPipeline.RemoveRecord(ref cache.C, cache.C.GridIndex[serial], batch.B);
                        expected.Remove(serial); Finalize(ref cache, ref batch);
                    }
                    else
                    {
                        Finalize(ref cache, ref batch);
                        if (first < total) ScheduleBricks(ref cache, ref batch, update, total - first);
                        foreach (var edit in edits) { if (edit.Value) expected[serial].Remove(edit.Key); else expected[serial].Add(edit.Key); }
                        Assembly(ref cache, ref batch, update, expected[serial].Count); Finalize(ref cache, ref batch);
                        ColliderPipeline.FreeUpdate(ref cache.C, update);
                    }
                    ColliderPipelineInvariants.Check(cache, batch);
                    foreach (var grid in expected)
                    {
                        var children = cache.C.Grids[cache.C.GridIndex[grid.Key]].Children;
                        Assert.AreEqual(grid.Value.Count, children.Length);
                        for (int i = 0; i < children.Length; i++) Assert.IsTrue(grid.Value.Contains(children[i].Coord));
                    }
                    DisposeRetired(ref cache);
                }
            }
            finally { batch.Dispose(); cache.Dispose(); }
        }
    }
}
