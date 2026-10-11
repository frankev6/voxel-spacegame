using System;
using System.Threading;
using Game.Core.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel.Physics
{
    public sealed class ColliderCleanupTests
    {
        static BlobAssetReference<Collider> Box() => BoxCollider.Create(new BoxGeometry
        { Size = new float3(1), Orientation = quaternion.identity }, PhysicsLayers.Grid, PhysicsLayers.GridMaterial);

        sealed class Fixture : IDisposable
        {
            public readonly World World = new World("ColliderCleanup");
            public EntityManager Em => World.EntityManager;
            public Entity Owner, Active, Clock;
            public SystemHandle Cleanup, Deferred;
            public ColliderBuildCache Cache;
            public ColliderBatchQueue Batch;
            public ColliderDirtyQueue Queue;
            public DeferredDisposalQueue Retired => Em.GetComponentData<DeferredDisposalQueue>(Owner);
            public Fixture(bool active = true, bool config = true)
            {
                World.CreateSystem<GridSpawnSystem>(); // Exercises the production singleton owner and OnDestroy.
                using var q = Em.CreateEntityQuery(typeof(ColliderBuildCache)); Owner = q.GetSingletonEntity();
                Cache = Em.GetComponentData<ColliderBuildCache>(Owner); Batch = Em.GetComponentData<ColliderBatchQueue>(Owner);
                Queue = Em.GetComponentData<ColliderDirtyQueue>(Owner);
                if (active) Active = Em.CreateSingleton<GameplayActive>();
                if (config) Em.CreateSingleton(PhysicsVoxelConfig.Default);
                Clock = Em.CreateSingleton(new SimClock { Tick = 20 });
                var retired = Retired; retired.Frame = 10; retired.Tick = 20; retired.HasClock = 1; Em.SetComponentData(Owner, retired);
                Cleanup = World.CreateSystem<GridColliderCleanupSystem>(); Deferred = World.CreateSystem<DeferredDisposalSystem>();
            }
            public Entity Record(uint serial)
            {
                Entity root = Em.CreateEntity(typeof(VoxelGrid), typeof(PhysicsCollider));
                Em.SetComponentData(root, new VoxelGrid { Serial = serial, CellSize = .5f });
                var installed = Box(); Em.SetComponentData(root, new PhysicsCollider { Value = installed });
                int index = ColliderPipeline.AddRecord(ref Cache.C, root, serial, .5f, GridMotion.Dynamic, installed: installed);
                var record = Cache.C.Grids[index]; var child = Box(); record.Children.Add(new BrickChild { Blob = child, BoxCount = 1 }); Cache.C.Grids[index] = record;
                int update = ColliderPipeline.GetOrCreateUpdate(ref Cache.C, serial, 8, 1, ColliderPriority.Damage);
                ColliderPipeline.Seal(ref Cache.C, update); var u = Cache.C.Updates[update]; u.State = UpdateState.Built;
                u.Compound = Box(); u.ChildCount = u.BoxCount = 1; Cache.C.Updates[update] = u;
                var stats = Cache.C.Stats.Value; stats.CachedBricks++; stats.CachedBoxes++; stats.CachedBrickBytes += ColliderPipeline.Bytes(child);
                stats.PendingCompoundBytes += ColliderPipeline.Bytes(u.Compound); Cache.C.Stats.Value = stats;
                return root;
            }
            public int Pending(uint serial, uint tick)
            {
                int update = ColliderPipeline.GetOrCreateUpdate(ref Cache.C, serial, tick, 1, ColliderPriority.Damage);
                var mask = new BrickMaskBits(); mask.Set(0, true);
                ColliderPipeline.SetSnapshot(ref Cache.C, update, new int3(1, 0, 0), mask, false); return update;
            }
            public int Schedule(uint serial)
            {
                int update = Pending(serial, 12); ColliderPipeline.Seal(ref Cache.C, update);
                Batch.B.Masks.Add(ColliderPipeline.TakeNextMask(ref Cache.C, update, out _));
                Batch.B.Bricks.Add(new BrickBuildItem { Update = update, Serial = serial, Coord = new int3(1, 0, 0), CellSize = .5f });
                Batch.B.Results.Add(default);
                Batch.B.AssemblyChildren.Add(Cache.C.Grids[Cache.C.GridIndex[serial]].Children[0]);
                Batch.B.AssemblyApply.Add(0);
                Batch.B.Assemblies.Add(new AssemblyItem { Update = update, Serial = serial, CellSize = .5f, ChildCount = 1, ApplyCount = 1 });
                Batch.B.AssemblyResults.Add(default);
                var delay = new DelayJob().Schedule();
                var bricks = new BrickColliderJob { Items = Batch.B.Bricks.AsArray(), Masks = Batch.B.Masks.AsArray(),
                    Results = Batch.B.Results.AsArray(), MaxBoxes = 64, Bevel = .05f }.Schedule(1, 1, delay);
                var assembly = new GridAssembleJob { Items = Batch.B.Assemblies.AsArray(), Children = Batch.B.AssemblyChildren.AsArray(),
                    Apply = Batch.B.AssemblyApply.AsArray(), Bricks = Batch.B.Bricks.AsArray(), BrickResults = Batch.B.Results.AsArray(),
                    Results = Batch.B.AssemblyResults.AsArray() }.Schedule(1, 1, bricks);
                Batch.State.Value = new ColliderBatchState { InFlight = 1, Handle = assembly, ScheduledFrame = Retired.Frame };
                JobHandle.ScheduleBatchedJobs(); return update;
            }
            public void Sweep() => Cleanup.Update(World.Unmanaged);
            public void Advance()
            {
                var clock = Em.GetComponentData<SimClock>(Clock); clock.Tick++; Em.SetComponentData(Clock, clock);
                Deferred.Update(World.Unmanaged);
            }
            public void Empty()
            {
                ColliderPipelineInvariants.Check(Cache, Batch);
                Assert.AreEqual(0, Cache.C.Grids.Length); Assert.AreEqual(0, Cache.C.GridIndex.Count);
                Assert.AreEqual(0, Cache.C.Stats.Value.PendingUpdates); Assert.AreEqual(0, Cache.C.Stats.Value.PendingBricks);
                Assert.AreEqual(Cache.C.Masks.Length, Cache.C.FreeMasks.Length); Assert.AreEqual(0, Cache.C.Retired.Length);
                Assert.AreEqual(0, Cache.C.Stats.Value.RetiredPendingBytes);
            }
            public void Dispose() => World.Dispose();
        }
        struct DelayJob : IJob { public void Execute() => Thread.Sleep(150); }

        [Test] public void DestroyedRoot_BlobsRetired()
        {
            using var f = new Fixture(); Entity root = f.Record(1); var original = f.Cache.C.Grids[0].Installed;
            int building = f.Schedule(1); f.Pending(1, 16); f.Em.DestroyEntity(root); f.Sweep();
            Assert.AreEqual(3, f.Retired.Blobs.Length, "Installed, cached child and built pending compound retire in this sweep");
            Assert.AreEqual(UpdateState.Discarded, f.Cache.C.Updates[building].State);
            Assert.AreEqual(1, f.Batch.State.Value.InFlight, "Young batches retain their slots until finalization");
            foreach (var blob in f.Retired.Blobs) { Assert.AreEqual(13, blob.DueFrame); Assert.AreEqual(22, blob.DueTick); }
            f.Advance(); f.Sweep(); Assert.AreEqual(3, f.Retired.Blobs.Length); Assert.AreEqual(ColliderType.Box, original.Value.Type);
            f.Advance(); f.Sweep(); // Age two forces completion before the borrowed child can be freed on frame 13.
            Assert.AreEqual(0, f.Batch.State.Value.InFlight); Assert.AreEqual(5, f.Retired.Blobs.Length); f.Empty();
            Assert.AreEqual(ColliderType.Box, original.Value.Type);
            f.Advance(); Assert.AreEqual(2, f.Retired.Blobs.Length);
            Assert.Throws<InvalidOperationException>(() => { var unused = original.Value.Type; });
            f.Advance(); Assert.AreEqual(2, f.Retired.Blobs.Length); f.Advance(); Assert.AreEqual(0, f.Retired.Blobs.Length);
            f.Sweep(); f.Advance(); Assert.AreEqual(0, f.Retired.Blobs.Length, "No duplicate ownership or retirement");
        }

        [Test] public void BatchDestroyByQuery_NoLeak()
        {
            void Exercise()
            {
                using var f = new Fixture();
                for (uint serial = 1; serial <= 32; serial++)
                {
                    Entity root = f.Record(serial); f.Pending(serial, 12); Entity child = f.Em.CreateEntity();
                    f.Em.AddComponentData(child, new VoxelGrid()); // Query includes the complete linked group, as unload does.
                    var linked = f.Em.AddBuffer<LinkedEntityGroup>(root); linked.Add(new LinkedEntityGroup { Value = root }); linked.Add(new LinkedEntityGroup { Value = child });
                }
                using var roots = f.Em.CreateEntityQuery(typeof(VoxelGrid)); f.Em.DestroyEntity(roots); f.Sweep(); f.Empty();
                Assert.AreEqual(96, f.Retired.Blobs.Length);
                f.Advance(); f.Advance(); Assert.AreEqual(96, f.Retired.Blobs.Length); f.Advance(); Assert.AreEqual(0, f.Retired.Blobs.Length);
            }
            NoLeaks(Exercise);
        }

        [Test] public void GameplayInactive_CacheEmptied()
        {
            using var f = new Fixture(); Entity root = f.Record(1); Entity child = f.Em.CreateEntity();
            var linked = f.Em.AddBuffer<LinkedEntityGroup>(root); linked.Add(new LinkedEntityGroup { Value = root }); linked.Add(new LinkedEntityGroup { Value = child });
            f.Schedule(1); f.Pending(1, 16); f.Queue.Enqueue(1, root, child, default, ColliderPriority.Damage);
            f.Em.DestroyEntity(root); Assert.IsFalse(f.Em.Exists(child), "The LEG cascade destroys dependent entities");
            f.Em.DestroyEntity(f.Active); f.Sweep(); f.Empty();
            Assert.AreEqual(0, f.Queue.Requests.Length); Assert.AreEqual(0, f.Batch.State.Value.InFlight); Assert.AreEqual(5, f.Retired.Blobs.Length);
            f.Advance(); f.Advance(); Assert.AreEqual(5, f.Retired.Blobs.Length); f.Advance(); Assert.AreEqual(0, f.Retired.Blobs.Length);
        }

        [Test] public void WorldDispose_BatchInFlight_NoLeak()
        {
            void Exercise()
            {
                using var f = new Fixture(); f.Record(1); f.Schedule(1); f.Pending(1, 16);
                Assert.IsFalse(f.Batch.State.Value.Handle.IsCompleted);
                // Dispose calls GridSpawnSystem.OnDestroy / VoxelSingletons.DisposeAll, with no cleanup update first.
            }
            NoLeaks(Exercise);
        }

        [Test] public void RunsWithoutGameplayActive()
        {
            using var f = new Fixture(active: false, config: false); f.Record(1); f.Schedule(1);
            f.Em.SetComponentData(f.Clock, new SimClock { Tick = 26 }); // Catch-up since the queue's previous-frame tick (20).
            f.Sweep(); f.Empty();
            Assert.AreEqual(0, f.Batch.State.Value.InFlight); Assert.AreEqual(5, f.Retired.Blobs.Length);
            foreach (var blob in f.Retired.Blobs) Assert.AreEqual(28, blob.DueTick);
            for (int i = 0; i < 4; i++) f.Deferred.Update(f.World.Unmanaged);
            Assert.AreEqual(5, f.Retired.Blobs.Length, "Paused physics still borrows the collider even after the frame delay");
            f.Advance(); Assert.AreEqual(5, f.Retired.Blobs.Length); f.Advance(); Assert.AreEqual(0, f.Retired.Blobs.Length);
        }

        static void NoLeaks(Action exercise)
        {
            var previous = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try { exercise(); UnsafeUtility.CheckForLeaks(); exercise(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks()); }
            finally { NativeLeakDetection.Mode = previous; }
        }
    }
}
