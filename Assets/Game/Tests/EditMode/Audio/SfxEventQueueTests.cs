using System;
using Game.Core;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Audio
{
    /// <summary>AUD-001: the SfxEvent queue singleton's lifetime (SPEC-64 §4.1).</summary>
    public sealed class SfxEventQueueTests
    {
        [Test]
        public void World_CreatesSingleton_AndDisposesWithoutLeak()
        {
            var world = new World("SfxEventQueueTests");
            try
            {
                world.CreateSystem<SfxEventQueueCreateSystem>();
                var em = world.EntityManager;
                var q = em.CreateEntityQuery(ComponentType.ReadOnly<SfxEventQueue>());   // owned by the world: disposed with it
                Assert.AreEqual(1, q.CalculateEntityCount());
                var queue = q.GetSingleton<SfxEventQueue>().Queue;
                Assert.IsTrue(queue.IsCreated);

                SfxEvents.EnqueueMainThread(em, SfxEvents.At(new ContentHash(7), new float3(1, 0, 0)));
                SfxEvents.EnqueueMainThread(em, SfxEvents.At(default, float3.zero));   // cue 0: ignored
                Assert.AreEqual(1, queue.Count);

                world.Dispose(); world = null;
                Assert.Catch<InvalidOperationException>(() => { var _ = queue.Count; }, "the queue was disposed with the world");
            }
            finally { world?.Dispose(); }
        }

        [Test]
        public void EnqueueMainThread_WithoutQueue_IsNoOp()
        {
            using var world = new World("NoQueue");
            Assert.DoesNotThrow(() => SfxEvents.EnqueueMainThread(world.EntityManager, SfxEvents.At(new ContentHash(7), float3.zero)));
        }
    }
}
