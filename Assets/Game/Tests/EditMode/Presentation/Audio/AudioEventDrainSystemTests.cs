using Game.Core;
using Game.Presentation.Audio;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;

namespace Game.Tests.EditMode.Presentation.Audio
{
    public sealed class AudioEventDrainSystemTests
    {
        [Test] public void Unbound_DrainsAndDiscards() => Check(7, 7, 0);
        [Test] public void HardCap_ClearsRemainder() => Check(10000, 4096, 5904);
        static void Check(int count, int processed, int excess)
        {
            using var world = new World("Audio drain tests");
            world.CreateSystem<SfxEventQueueCreateSystem>();
            var system = world.GetOrCreateSystemManaged<AudioEventDrainSystem>();
            using var query = world.EntityManager.CreateEntityQuery(typeof(SfxEventQueue));
            var queue = query.GetSingleton<SfxEventQueue>().Queue;
            for (int i = 0; i < count; i++) queue.Enqueue(new SfxEvent { Cue = new ContentHash(1), Volume = 1 });
            system.Update();
            Assert.IsTrue(queue.IsEmpty()); Assert.AreEqual(processed, system.Stats.EventsDrained);
            Assert.AreEqual(excess, system.Stats.EventsOverHardCap); Assert.IsFalse(system.Stats.Bound);
        }
    }
}
