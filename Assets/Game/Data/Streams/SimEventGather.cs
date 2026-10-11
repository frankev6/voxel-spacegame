using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Data.Streams
{
    /// <summary>Burst helper for consumers: appends every event of a read set to output, registrations in order, foreach indices ascending.
    /// Consumers then stable-sort with their own key (the gather order is the tie-break). Implements SPEC-02 §4.12, §5.13.
    /// One job per registration, chained in registration order: each holds a single NativeStream.Reader field, so the safety system
    /// tracks every stream (SPEC-02 open question 11).</summary>
    public static class SimEventGather
    {
        public static JobHandle Schedule<T>(SimEventReadSet set, NativeList<T> output, JobHandle dependsOn) where T : unmanaged
        {
            var handle = dependsOn;
            for (int r = 0; r < set.Count; r++)
                handle = new Job<T> { Reader = set.Reader(r), ForEachCount = set.ForEachCounts[r], Output = output }.Schedule(handle);
            return handle;
        }

        /// <summary>Appends one registration's events. A player build needs
        /// <c>[assembly: Unity.Jobs.RegisterGenericJobType(typeof(SimEventGather.Job&lt;TEvent&gt;))]</c> once per event type, next to the
        /// channel's owner (SPEC-02 open question 11).</summary>
        [BurstCompile]
        public struct Job<T> : IJob where T : unmanaged
        {
            [ReadOnly] public NativeStream.Reader Reader;
            public int ForEachCount;
            public NativeList<T> Output;

            public void Execute()
            {
                for (int index = 0; index < ForEachCount; index++)
                {
                    int count = Reader.BeginForEachIndex(index);
                    for (int e = 0; e < count; e++) Output.Add(Reader.Read<T>());
                    Reader.EndForEachIndex();
                }
            }
        }
    }
}
