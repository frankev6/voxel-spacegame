using Game.Core;
using Game.Tests.Common;
using Game.Presentation.Audio;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class AudioEventDrainSystemTests
    {
        [Test] public void Unbound_DrainsAndDiscards() => Check(7, 7, 0);
        [Test] public void HardCap_ClearsRemainder() => Check(10000, 4096, 5904);
        struct Producer : IJob
        {
            [NativeDisableContainerSafetyRestriction] public NativeQueue<SfxEvent>.ParallelWriter Queue;
            public int Count;
            public void Execute() { for (int i = 0; i < Count; i++) Queue.Enqueue(new SfxEvent { Cue = new ContentHash(1), Volume = 1 }); }
        }
        static void Check(int count, int processed, int excess)
        {
            using var fixture = EcsTestWorld.Create(typeof(SfxEventQueueCreateSystem), typeof(AudioEventDrainSystem));
            var world = fixture.World;
            var system = world.GetOrCreateSystemManaged<AudioEventDrainSystem>();
            using var query = world.EntityManager.CreateEntityQuery(typeof(SfxEventQueue));
            var stream = query.GetSingleton<SfxEventQueue>(); var queue = stream.Queue;
            var producer = new Producer { Queue = queue.AsParallelWriter(), Count = count }
                .Schedule(stream.Access.BeginRead(default));
            stream.Access.EndRead(producer); // Deliberately not attached to an ECS component job dependency.

            system.Update();
            Assert.IsTrue(queue.IsEmpty()); Assert.AreEqual(processed, system.Stats.EventsDrained);
            Assert.AreEqual(excess, system.Stats.EventsOverHardCap); Assert.IsFalse(system.Stats.Bound);
        }
    }
}
