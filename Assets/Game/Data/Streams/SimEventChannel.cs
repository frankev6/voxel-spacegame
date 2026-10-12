using Game.Data.Combat;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Data.Streams
{
    /// <summary>Per-tick registry of deterministic simulation event streams of one event type (architecture §5.1). Any number of
    /// producers register a NativeStream each, in call order (= system update order, deterministic); exactly one consumer reads every
    /// registration in that order. The struct holds only a pointer, so the owning singleton is taken with SystemAPI.GetSingleton
    /// (read-only) by producers and consumer alike: no sync points. Implements SPEC-02 §4.12; algorithm §5.13.</summary>
    public unsafe struct SimEventChannel<T> where T : unmanaged
    {
        [NativeDisableUnsafePtrRestriction] internal SimEventChannelData<T>* Data;

        /// <summary>Allocator.Persistent; name for logs.</summary>
        public static SimEventChannel<T> Create(FixedString32Bytes name)
        {
            var data = (SimEventChannelData<T>*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<SimEventChannelData<T>>(),
                UnsafeUtility.AlignOf<SimEventChannelData<T>>(), Allocator.Persistent, 0);
            UnsafeUtility.MemClear(data, UnsafeUtility.SizeOf<SimEventChannelData<T>>());
            data->Pending = new UnsafeList<SimEventRegistration>(4, Allocator.Persistent);
            data->Inbox = new NativeList<T>(4, Allocator.Persistent);
            data->ReadStreams = new UnsafeList<NativeStream>(4, Allocator.Persistent);
            data->Name = name;
            return new SimEventChannel<T> { Data = data };
        }

        public bool IsCreated => Data != null;

        /// <summary>Completes every stored handle, disposes every stream still registered. No-op if !IsCreated.</summary>
        public void Dispose()
        {
            if (Data == null) return;
            Data->LastConsumer.Complete();
            // An abandoned BeginRead may still include running producers. Complete them before freeing its set.
            for (int i = 0; i < Data->Pending.Length; i++) Data->Pending[i].Writer.Complete();
            if (Data->OpenRead != 0) DisposeReadSet(default).Complete();
            for (int i = 0; i < Data->Pending.Length; i++) Data->Pending[i].Stream.Dispose();
            Data->Pending.Dispose();
            Data->Inbox.Dispose();
            Data->ReadStreams.Dispose();
            UnsafeUtility.FreeTracked(Data, Allocator.Persistent);
            Data = null;
        }

        /// <summary>Channel owner, once per tick before producers or the consumer run (or anywhere in the tick; see §5.13 step 1).
        /// Disposes registrations written at least one full tick ago and never read (the consumer did not run), warning once per channel.</summary>
        public void BeginTick(uint tick)
        {
            CheckCreated();
            int first = Data->OpenRead != 0 ? Data->Taken : 0;     // never pull streams out from under an open read set
            bool removed = false;
            for (int i = Data->Pending.Length - 1; i >= first; i--)
            {
                var r = Data->Pending[i];
                if (r.Tick + 1 >= tick) continue;
                if (Data->OpenWrite != 0 && i == Data->Pending.Length - 1) continue;
                Data->LastConsumer = JobHandle.CombineDependencies(Data->LastConsumer, r.Stream.Dispose(r.Writer));
                Data->Pending.RemoveAt(i);
                removed = true;
            }
            if (removed && Data->StaleWarned == 0)
            {
                Data->StaleWarned = 1;
                // Construct the warning without a managed-string conversion: BeginTick is called from Burst owners.
                var msg = default(FixedString128Bytes);
                msg.Append('['); msg.Append('C'); msg.Append('o'); msg.Append('r'); msg.Append('e'); msg.Append(']'); msg.Append(' ');
                msg.Append('s'); msg.Append('t'); msg.Append('a'); msg.Append('l'); msg.Append('e'); msg.Append(' ');
                msg.Append('s'); msg.Append('t'); msg.Append('r'); msg.Append('e'); msg.Append('a'); msg.Append('m'); msg.Append(':'); msg.Append(' ');
                msg.Append(Data->Name);
                Game.Core.GameLog.Burst.Warn(msg);
            }
            Data->Tick = tick;
        }

        /// <summary>Producer, main thread inside OnUpdate: registers a new NativeStream (Allocator.TempJob) with forEachCount indices.</summary>
        public NativeStream.Writer BeginWrite(ref SystemState state, ProducerKind producer, int forEachCount)
        {
            CheckCreated();
            if (forEachCount < 1) throw new System.ArgumentOutOfRangeException(nameof(forEachCount), forEachCount, "SimEventChannel.BeginWrite needs forEachCount >= 1");
            if (Data->OpenWrite != 0) throw new System.InvalidOperationException($"SimEventChannel {Data->Name}: BeginWrite called twice without EndWrite");
            var stream = new NativeStream(forEachCount, Allocator.TempJob);
            Data->Pending.Add(new SimEventRegistration { Stream = stream, Producer = producer, ForEachCount = forEachCount, Tick = Data->Tick });
            Data->OpenWrite = 1;
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, Data->LastConsumer);
            return stream.AsWriter();
        }

        /// <summary>Producer: exactly once after scheduling the job that writes the stream returned by the matching BeginWrite.</summary>
        public void EndWrite(JobHandle writerJob)
        {
            CheckCreated();
            if (Data->OpenWrite == 0) throw new System.InvalidOperationException($"SimEventChannel {Data->Name}: EndWrite without BeginWrite");
            ref var last = ref Data->Pending.ElementAt(Data->Pending.Length - 1);
            last.Writer = writerJob;
            Data->OpenWrite = 0;
        }

        /// <summary>Consumer, main thread inside OnUpdate: every registration not yet read, in registration order. Combines all their
        /// writer handles into state.Dependency. Registrations made after this call are read by the next BeginRead.</summary>
        public SimEventReadSet BeginRead(ref SystemState state)
        {
            CheckCreated();
            if (Data->OpenRead != 0) throw new System.InvalidOperationException($"SimEventChannel {Data->Name}: BeginRead called twice without EndRead");
            if (Data->OpenWrite != 0) throw new System.InvalidOperationException($"SimEventChannel {Data->Name}: BeginRead during an open BeginWrite");
            if (Data->Inbox.Length > 0) MoveInboxToFront();

            int k = Data->Pending.Length;
            var set = new SimEventReadSet
            {
                Streams = &Data->ReadStreams,
                Producers = new NativeArray<ProducerKind>(k, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                ForEachCounts = new NativeArray<int>(k, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
            };
            var dep = state.Dependency;
            for (int i = 0; i < k; i++)
            {
                var r = Data->Pending[i];
                Data->ReadStreams.Add(r.Stream);
                set.Producers[i] = r.Producer;
                set.ForEachCounts[i] = r.ForEachCount;
                dep = JobHandle.CombineDependencies(dep, r.Writer);
            }
            state.Dependency = dep;
            Data->Taken = k;
            Data->OpenRead = 1;
            Data->ReadSet = set;
            return set;
        }

        /// <summary>Consumer: exactly once after scheduling the last job that uses the read set; disposes those streams and the set after it.</summary>
        public void EndRead(JobHandle readerJob)
        {
            CheckCreated();
            if (Data->OpenRead == 0) throw new System.InvalidOperationException($"SimEventChannel {Data->Name}: EndRead without BeginRead");
            // Own both the reader and every asynchronous deallocation, including older stale cleanup.
            Data->LastConsumer = DisposeReadSet(JobHandle.CombineDependencies(Data->LastConsumer, readerJob));
        }

        /// <summary>Registrations not yet read (tests, harness).</summary>
        public int PendingCount { get { CheckCreated(); return Data->Pending.Length - (Data->OpenRead != 0 ? Data->Taken : 0); } }

        /// <summary>Main thread OUTSIDE FixedStepSimulationSystemGroup (narrative effects, services, dev commands; ADR-0016): appends one event to the
        /// channel's inbox, read by the consumer's next BeginRead as the first registration (ProducerKind.MainThread). Not frame-rate deterministic:
        /// never used by replayed simulation.</summary>
        public void PostFromMainThread(in T evt)
        {
            CheckCreated();
            Data->Inbox.Add(evt);
        }

        void MoveInboxToFront()
        {
            var stream = new NativeStream(1, Allocator.TempJob);
            var w = stream.AsWriter();
            w.BeginForEachIndex(0);
            for (int i = 0; i < Data->Inbox.Length; i++) w.Write(Data->Inbox[i]);
            w.EndForEachIndex();
            Data->Inbox.Clear();
            Data->Pending.InsertRange(0, 1);
            Data->Pending[0] = new SimEventRegistration { Stream = stream, Producer = ProducerKind.MainThread, ForEachCount = 1, Tick = Data->Tick };
        }

        JobHandle DisposeReadSet(JobHandle after)
        {
            var cleanup = after;
            for (int i = 0; i < Data->Taken; i++)
                cleanup = JobHandle.CombineDependencies(cleanup, Data->Pending[i].Stream.Dispose(after));
            Data->Pending.RemoveRange(0, Data->Taken);
            Data->ReadStreams.Clear();
            cleanup = JobHandle.CombineDependencies(cleanup, Data->ReadSet.Producers.Dispose(after), Data->ReadSet.ForEachCounts.Dispose(after));
            Data->ReadSet = default;
            Data->Taken = 0;
            Data->OpenRead = 0;
            return cleanup;
        }

        void CheckCreated()
        {
            if (Data == null) throw new System.ObjectDisposedException("SimEventChannel", "the channel is not created or was disposed");
        }
    }
}
