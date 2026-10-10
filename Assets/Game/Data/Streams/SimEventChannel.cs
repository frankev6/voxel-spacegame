namespace Game.Data.Streams
{
    /// <summary>Per-tick registry of deterministic simulation event streams of one event type (architecture §5.1). Any number of
    /// producers register a NativeStream each, in call order (= system update order, deterministic); exactly one consumer reads every
    /// registration in that order. The struct holds only a pointer, so the owning singleton is taken with SystemAPI.GetSingleton
    /// (read-only) by producers and consumer alike: no sync points. Implements SPEC-02 §4.12; algorithm §5.13.</summary>
    public unsafe struct SimEventChannel<T> where T : unmanaged
    {
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] internal SimEventChannelData<T>* Data;

        public static SimEventChannel<T> Create(Unity.Collections.FixedString32Bytes name) { throw new System.NotImplementedException("CORE-012"); }   // Allocator.Persistent; name for logs
        public bool IsCreated => Data != null;
        public void Dispose() { throw new System.NotImplementedException("CORE-012"); }                                 // completes every stored handle, disposes every stream still registered

        /// <summary>Channel owner, once per tick before producers or the consumer run (or anywhere in the tick; see §5.13 step 1).</summary>
        public void BeginTick(uint tick) { throw new System.NotImplementedException("CORE-012"); }

        /// <summary>Producer, main thread inside OnUpdate: registers a new NativeStream (Allocator.TempJob) with forEachCount indices.</summary>
        public Unity.Collections.NativeStream.Writer BeginWrite(ref Unity.Entities.SystemState state, Game.Data.Combat.ProducerKind producer, int forEachCount) { throw new System.NotImplementedException("CORE-012"); }
        /// <summary>Producer: exactly once after scheduling the job that writes the stream returned by the matching BeginWrite.</summary>
        public void EndWrite(Unity.Jobs.JobHandle writerJob) { throw new System.NotImplementedException("CORE-012"); }

        /// <summary>Consumer, main thread inside OnUpdate: every registration not yet read, in registration order. Combines all their
        /// writer handles into state.Dependency. Registrations made after this call are read by the next BeginRead.</summary>
        public SimEventReadSet BeginRead(ref Unity.Entities.SystemState state) { throw new System.NotImplementedException("CORE-012"); }
        /// <summary>Consumer: exactly once after scheduling the last job that uses the read set; disposes those streams and the set after it.</summary>
        public void EndRead(Unity.Jobs.JobHandle readerJob) { throw new System.NotImplementedException("CORE-012"); }

        public int PendingCount => throw new System.NotImplementedException("CORE-012");                       // registrations not yet read (tests, harness)

        /// <summary>Main thread OUTSIDE FixedStepSimulationSystemGroup (narrative effects, services, dev commands; ADR-0016): appends one event to the
        /// channel's inbox, read by the consumer's next BeginRead as the first registration (ProducerKind.MainThread). Not frame-rate deterministic:
        /// never used by replayed simulation.</summary>
        public void PostFromMainThread(in T evt) { throw new System.NotImplementedException("CORE-012"); }
    }

    /// <summary>Heap block behind a SimEventChannel (internal, SPEC-02 §4.12, §5.13).</summary>
    internal struct SimEventChannelData<T> where T : unmanaged
    {
        public Unity.Collections.LowLevel.Unsafe.UnsafeList<SimEventRegistration> Pending;
        public Unity.Collections.FixedString32Bytes Name;
        public uint Tick;
        public Unity.Jobs.JobHandle LastConsumer;
        public byte OpenWrite;
        public byte StaleWarned;
        public Unity.Collections.NativeList<T> Inbox;          // PostFromMainThread
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
