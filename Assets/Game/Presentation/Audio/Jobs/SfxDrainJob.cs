using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Presentation.Audio
{
    internal enum SfxCounter { Drained, OverCap, Unresolved, Silent, Culled, Groups, CooldownDropped, CombatSignals, Count }

    /// <summary>SPEC-64 §5.3: bounded frame drain; every queued event is consumed or counted as over cap.</summary>
    [BurstCompile] internal struct SfxDrainJob : IJob
    {
        public NativeQueue<SfxEvent> Queue;
        public NativeList<SfxEvent> Drained;
        public NativeArray<int> Counters;
        public void Execute()
        {
            Drained.Clear();
            for (int i = 0; i < Counters.Length; i++) Counters[i] = 0;
            while (Drained.Length < 4096 && Queue.TryDequeue(out var value)) Drained.Add(value);
            Counters[(int)SfxCounter.Drained] = Drained.Length;
            Counters[(int)SfxCounter.OverCap] = Queue.Count;
            Queue.Clear();
        }
    }
}
