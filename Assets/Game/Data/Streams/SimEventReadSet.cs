using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Data.Streams
{
    /// <summary>This tick's registrations of one channel, in registration order. Arrays are Allocator.TempJob, disposed by EndRead.
    /// Readers are handed out one per job field through <see cref="Reader"/>: a NativeArray of NativeStream.Reader would be a nested native
    /// container, which jobs reject (SPEC-02 open question 11). Implements SPEC-02 §4.12.</summary>
    public unsafe struct SimEventReadSet
    {
        [ReadOnly] public NativeArray<Game.Data.Combat.ProducerKind> Producers;
        [ReadOnly] public NativeArray<int> ForEachCounts;
        [NativeDisableUnsafePtrRestriction] internal UnsafeList<NativeStream>* Streams;   // the channel's taken streams, valid until EndRead
        public int Count => Producers.Length;

        /// <summary>Main thread, between BeginRead and EndRead: the reader of registration <paramref name="index"/>, to put in a job field.</summary>
        public NativeStream.Reader Reader(int index)
        {
            if (Streams == null || (uint)index >= (uint)Count) throw new System.ArgumentOutOfRangeException(nameof(index), index, "SimEventReadSet.Reader");
            return (*Streams)[index].AsReader();
        }
    }
}
