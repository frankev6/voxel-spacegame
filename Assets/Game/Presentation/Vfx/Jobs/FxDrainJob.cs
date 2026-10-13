using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
namespace Game.Presentation.Vfx
{
    [BurstCompile]
    internal struct FxDrainJob : IJob
    {
        internal const int HardCap = 65536;
        public NativeQueue<FxEvent> Queue;
        public NativeList<FxEvent> Drained;
        public NativeArray<int> Counters;
        public void Execute()
        {
            Drained.Clear();
            while (Drained.Length < HardCap && Queue.TryDequeue(out var e)) Drained.Add(e);
            Counters[(int)FxCounter.EventsOverHardCap] = Queue.Count;
            Queue.Clear();
        }
    }
}
