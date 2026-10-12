using System;
using Game.Tests.Common;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Tests.EditMode.Vfx
{
    /// <summary>SPEC-63 §4.1 singleton ownership, including outstanding native writers (ADR-0050).</summary>
    public sealed class FxEventQueueTests : GameTestBase
    {
        [Test] public void World_CreatesSingleton_AndDisposesWithoutLeak()
        {
            using (var warm = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem))) warm.StepFrame(1f / 60);
            UnsafeUtility.CheckForLeaks(); // Clear global world/Burst warm-up allocations before the measured lifetime.
            FxEventQueue stream;
            using (var world = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem)))
            {
                using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<FxEventQueue>());
                Assert.AreEqual(1, query.CalculateEntityCount());
                stream = query.GetSingleton<FxEventQueue>();
                Assert.IsTrue(stream.Queue.IsCreated); Assert.IsTrue(stream.Access.IsCreated);
                for (int i = 0; i < 2; i++)
                {
                    var job = new EnqueueJob { Queue = stream.Queue.AsParallelWriter() }.Schedule(stream.Access.BeginRead(default));
                    stream.Access.EndRead(job);
                }
                FxEvents.EnqueueMainThread(world.EntityManager, FxEvents.Explosion(float3.zero, 2, float3.zero));
                Assert.AreEqual(257, stream.Queue.Count, "Main-thread enqueue waits for both registered writers");
                var final = new EnqueueJob { Queue = stream.Queue.AsParallelWriter() }.Schedule(stream.Access.BeginRead(default));
                stream.Access.EndRead(final); // Deliberately not completed here: the world owns its lifetime.
            }
            Assert.Catch<InvalidOperationException>(() => { var unused = stream.Queue.Count; }, "Queue freed by world teardown");
            Assert.Catch<InvalidOperationException>(() => { var unused = stream.Access.Exclusive.Value; }, "Guard freed by world teardown");
            Assert.AreEqual(0, UnsafeUtility.CheckForLeaks(), "Queue and both guard references are disposed");
        }
        struct EnqueueJob : IJob
        {
            // Only atomic ParallelWriter enqueue is used; registered shared handles prevent drain/disposal overlap.
            [NativeDisableContainerSafetyRestriction] public NativeQueue<FxEvent>.ParallelWriter Queue;
            public void Execute()
            { for (int i = 0; i < 128; i++) Queue.Enqueue(FxEvents.Explosion(new float3(i, 0, 0), 2, float3.zero)); }
        }
    }
}
