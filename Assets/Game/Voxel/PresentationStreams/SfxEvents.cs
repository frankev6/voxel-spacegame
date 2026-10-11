using Game.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.PresentationStreams
{
    /// <summary>Builds and enqueues SfxEvents. Implements SPEC-64 §4.1.</summary>
    public static class SfxEvents
    {
        public static SfxEvent At(ContentHash cue, float3 pos, float volume = 1f, bool player = false) =>
            new SfxEvent { Cue = cue, Pos = pos, Volume = math.saturate(volume), Flags = player ? SfxEventFlags.PlayerSource : SfxEventFlags.None };

        /// <summary>Main-thread enqueue for managed producers. No-op when the world has no queue or cue.Value == 0.</summary>
        public static void EnqueueMainThread(EntityManager em, in SfxEvent e)
        {
            if (e.Cue.Value == 0) return;
            using var q = em.CreateEntityQuery(ComponentType.ReadWrite<SfxEventQueue>());
            if (q.IsEmpty) return;
            em.CompleteDependencyBeforeRW<SfxEventQueue>();
            q.GetSingleton<SfxEventQueue>().Queue.Enqueue(e);
        }
    }
}
