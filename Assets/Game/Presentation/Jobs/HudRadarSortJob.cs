using Game.Presentation.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Presentation.Jobs
{
    /// <summary>SPEC-61 §5.1: orders the bounded contact set by distance and entity index, then publishes it.</summary>
    [BurstCompile]
    public struct HudRadarSortJob : IJob
    {
        public NativeArray<HudRadarCandidate> Heap;
        [ReadOnly] public NativeReference<int> Count;
        public BufferLookup<HudRadarBlip> Blips;
        public Entity Snapshot;
        public void Execute()
        {
            int count = Count.Value;
            for (int i = 1; i < count; i++)
            {
                var value = Heap[i]; int j = i - 1;
                while (j >= 0 && HudRadarCandidate.Farther(Heap[j], value)) { Heap[j + 1] = Heap[j]; j--; }
                Heap[j + 1] = value;
            }
            var buffer = Blips[Snapshot]; buffer.Clear();
            for (int i = 0; i < count; i++) buffer.Add(Heap[i].Blip);
        }
    }
}
