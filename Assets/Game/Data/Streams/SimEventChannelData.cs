namespace Game.Data.Streams
{
    /// <summary>Heap block behind a SimEventChannel (internal, SPEC-02 §4.12, §5.13). One Persistent allocation per channel; every
    /// copy of the channel struct points at the same block.</summary>
    internal struct SimEventChannelData<T> where T : unmanaged
    {
        public Unity.Collections.LowLevel.Unsafe.UnsafeList<SimEventRegistration> Pending;
        public Unity.Collections.FixedString32Bytes Name;
        public uint Tick;
        public Unity.Jobs.JobHandle LastConsumer;
        public byte OpenWrite;
        public byte StaleWarned;
        public Unity.Collections.NativeList<T> Inbox;          // PostFromMainThread
        public byte OpenRead;                                  // BeginRead without EndRead yet
        public int Taken;                                      // registrations handed to the open read set (Pending[0..Taken))
        public SimEventReadSet ReadSet;                        // the open read set, disposed by EndRead (or Dispose)
        public Unity.Collections.LowLevel.Unsafe.UnsafeList<Unity.Collections.NativeStream> ReadStreams;   // streams behind ReadSet.Reader
    }

    /// <summary>One producer's stream in a channel (internal, SPEC-02 §4.12 "Registration", §5.13).</summary>
    internal struct SimEventRegistration
    {
        public Unity.Collections.NativeStream Stream;
        public Unity.Jobs.JobHandle Writer;
        public Game.Data.Combat.ProducerKind Producer;
        public int ForEachCount;
        public uint Tick;
    }
}
