namespace Game.Data.Streams
{
    /// <summary>This tick's registrations of one channel, in registration order. Arrays are Allocator.TempJob, disposed by EndRead.
    /// Implements SPEC-02 §4.12.</summary>
    public struct SimEventReadSet
    {
        [Unity.Collections.ReadOnly] public Unity.Collections.NativeArray<Unity.Collections.NativeStream.Reader> Readers;
        [Unity.Collections.ReadOnly] public Unity.Collections.NativeArray<Game.Data.Combat.ProducerKind> Producers;
        [Unity.Collections.ReadOnly] public Unity.Collections.NativeArray<int> ForEachCounts;
        public int Count => Readers.Length;
    }
}
