using Game.Core.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Tests.EditMode.Voxel
{
    /// <summary>VOX-016: cleanup must wait for physics to replace its world, and must run without GameplayActive.</summary>
    public sealed class DeferredDisposalTests
    {
        static BlobAssetReference<Collider> Box() => BoxCollider.Create(new BoxGeometry
        { Center = float3.zero, Size = new float3(1), Orientation = quaternion.identity, BevelRadius = 0 }, PhysicsLayers.Grid, PhysicsLayers.GridMaterial);
        static Entity Owner(World world)
        {
            world.CreateSystem<GridSpawnSystem>();
            using var query = world.EntityManager.CreateEntityQuery(typeof(DeferredDisposalQueue));
            return query.GetSingletonEntity();
        }
        [Test] public void Blob_DisposedAfterThreeFrames()
        {
            using var world = new World("DeferredBlobFrames"); var em = world.EntityManager;
            Entity owner = Owner(world); var system = world.CreateSystem<DeferredDisposalSystem>();
            var queue = em.GetComponentData<DeferredDisposalQueue>(owner); var blob = Box();
            DeferredDisposal.RetireBlob(ref queue, blob);
            Assert.AreEqual(3u, queue.Blobs[0].DueFrame); Assert.AreEqual(2u, queue.Blobs[0].DueTick);
            for (int i = 0; i < 2; i++)
            {
                system.Update(world.Unmanaged); Assert.AreEqual(1, queue.Blobs.Length);
                Assert.AreEqual(ColliderType.Box, blob.Value.Type);
            }
            system.Update(world.Unmanaged); Assert.AreEqual(0, queue.Blobs.Length);
            // A copied BlobAssetReference retains its pointer; the safety check proves the allocation was disposed.
            Assert.Throws<System.InvalidOperationException>(() => { var unused = blob.Value.Type; });
        }
        [Test] public void Blob_WaitsForTicksWhilePaused()
        {
            using var world = new World("DeferredBlobPaused"); var em = world.EntityManager;
            Entity owner = Owner(world), clock = em.CreateSingleton(new SimClock { Tick = 20 });
            var system = world.CreateSystem<DeferredDisposalSystem>(); system.Update(world.Unmanaged);
            var queue = em.GetComponentData<DeferredDisposalQueue>(owner); var blob = Box();
            DeferredDisposal.RetireBlob(ref queue, blob); Assert.AreEqual(22u, queue.Blobs[0].DueTick);
            for (int i = 0; i < 100; i++) system.Update(world.Unmanaged);
            Assert.AreEqual(1, queue.Blobs.Length); Assert.AreEqual(ColliderType.Box, blob.Value.Type);
            em.SetComponentData(clock, new SimClock { Tick = 21 }); system.Update(world.Unmanaged);
            Assert.AreEqual(1, queue.Blobs.Length);
            em.SetComponentData(clock, new SimClock { Tick = 22 }); system.Update(world.Unmanaged);
            Assert.AreEqual(0, queue.Blobs.Length);
        }
        [Test] public void WorldDispose_NoLeaks()
        {
            void Exercise()
            {
                using var world = new World("DeferredPendingTeardown"); var em = world.EntityManager;
                Entity owner = Owner(world); world.CreateSystem<DeferredDisposalSystem>();
                var queue = em.GetComponentData<DeferredDisposalQueue>(owner);
                for (int i = 0; i < 12; i++) DeferredDisposal.RetireBlob(ref queue, Box());
                Assert.AreEqual(12, queue.Blobs.Length);
            }
            var previous = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try { Exercise(); UnsafeUtility.CheckForLeaks(); Exercise(); Assert.AreEqual(0, UnsafeUtility.CheckForLeaks()); }
            finally { NativeLeakDetection.Mode = previous; }
        }
    }
}
