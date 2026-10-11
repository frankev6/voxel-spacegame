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
    /// <summary>PHY-001: all pipeline containers and owned collider blobs are released at teardown.</summary>
    public sealed class PipelineSingletonTests
    {
        struct QueueWriteJob : IJob
        {
            public NativeList<ColliderRequest> Requests;
            public void Execute() { Requests.Add(default); }
        }
        struct CacheWriteJob : IJob
        {
            public NativeReference<uint> NextOrder;
            public void Execute() { NextOrder.Value = 1; }
        }
        struct BatchBuildJob : IJob
        {
            public NativeList<BrickBuildResult> Results;
            public void Execute() { Results.Add(new BrickBuildResult { Blob = Box() }); }
        }
        static BlobAssetReference<Collider> Box() => BoxCollider.Create(new BoxGeometry
        { Center = float3.zero, Size = new float3(1), Orientation = quaternion.identity, BevelRadius = 0 },
            PhysicsLayers.Grid, PhysicsLayers.GridMaterial);

        [Test]
        public void CreateDispose_NoLeaks()
        {
            Exercise(); // Warm up job reflection and Physics before resetting the leak counter.
            UnsafeUtility.CheckForLeaks();
            Exercise();
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks());
        }

        static void Exercise()
        {
            var queue = ColliderDirtyQueue.Create();
            var cache = ColliderBuildCache.Create();
            var batch = ColliderBatchQueue.Create();
            try
            {
                Assert.AreEqual(1024, queue.Requests.Capacity);
                Assert.AreEqual(256, cache.C.GridIndex.Capacity);
                Assert.AreEqual(256, cache.C.Updates.Capacity);
                Assert.AreEqual(256, cache.C.Masks.Capacity);
                Assert.AreEqual(512, batch.B.Bricks.Capacity);
                Assert.AreEqual(64, batch.B.Assemblies.Capacity);
                var record = new GridColliderRecord
                {
                    Installed = Box(),
                    Children = new UnsafeList<BrickChild>(1, Allocator.Persistent),
                    Updates = new UnsafeList<int>(1, Allocator.Persistent),
                };
                record.Children.Add(new BrickChild { Blob = Box(), BoxCount = 1 });
                record.Updates.Add(0);
                cache.C.Grids.Add(record);
                var update = new ColliderUpdate
                {
                    State = UpdateState.Built, Compound = Box(),
                    Bricks = new UnsafeList<UpdateBrick>(1, Allocator.Persistent),
                };
                update.Bricks.Add(default);
                cache.C.Updates.Add(update);
                cache.C.Retired.Add(Box());
                batch.B.AssemblyResults.Add(new AssemblyResult { Compound = Box() });
                // The batch borrows this child: disposing it here would double-free the cache's blob.
                batch.B.AssemblyChildren.Add(record.Children[0]);
                queue.Release(new QueueWriteJob { Requests = queue.Requests }.Schedule(queue.Acquire(default)));
                cache.Release(new CacheWriteJob { NextOrder = cache.C.NextCreateOrder }.Schedule(cache.Acquire(default)));
                batch.State.Value = new ColliderBatchState
                {
                    InFlight = 1,
                    Handle = new BatchBuildJob { Results = batch.B.Results }.Schedule(),
                };
            }
            finally { batch.Dispose(); cache.Dispose(); queue.Dispose(); }
        }
    }
}
