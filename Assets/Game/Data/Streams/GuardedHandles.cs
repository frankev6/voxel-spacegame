namespace Game.Data.Streams
{
    /// <summary>Job-handle guard embedded in every singleton that owns native memory shared by several systems (architecture §5.1;
    /// the access pattern of SPEC-12 §5.1). Main thread only; never passed into a job. Implements SPEC-02 §4.12.</summary>
    public struct GuardedHandles
    {
        public Unity.Collections.NativeReference<Unity.Jobs.JobHandle> Exclusive;   // last job with exclusive access (writers)
        public Unity.Collections.NativeReference<Unity.Jobs.JobHandle> Readers;     // shared readers since the last exclusive access

        /// <summary>Allocator.Persistent, both handles default.</summary>
        public static GuardedHandles Create() => new GuardedHandles
        {
            Exclusive = new Unity.Collections.NativeReference<Unity.Jobs.JobHandle>(Unity.Collections.Allocator.Persistent),
            Readers = new Unity.Collections.NativeReference<Unity.Jobs.JobHandle>(Unity.Collections.Allocator.Persistent),
        };
        public bool IsCreated => Exclusive.IsCreated;
        /// <summary>Exclusive access: waits for the last writer and every reader. Pair with exactly one Release.</summary>
        public Unity.Jobs.JobHandle Acquire(Unity.Jobs.JobHandle dependsOn)
            => Unity.Jobs.JobHandle.CombineDependencies(dependsOn, Exclusive.Value, Readers.Value);
        /// <summary>lastJob must depend on the handle Acquire returned.</summary>
        public void Release(Unity.Jobs.JobHandle lastJob) { Exclusive.Value = lastJob; Readers.Value = default; }
        /// <summary>Shared read access: waits for the last writer only; readers run in parallel. Pair with exactly one EndRead.</summary>
        public Unity.Jobs.JobHandle BeginRead(Unity.Jobs.JobHandle dependsOn)
            => Unity.Jobs.JobHandle.CombineDependencies(dependsOn, Exclusive.Value);
        public void EndRead(Unity.Jobs.JobHandle readerJob)
        { Readers.Value = Unity.Jobs.JobHandle.CombineDependencies(Readers.Value, readerJob); }
        /// <summary>Main thread outside FixedStepSimulationSystemGroup only: completes every guarded job.</summary>
        public void CompleteAccess() { Exclusive.Value.Complete(); Readers.Value.Complete(); }
        /// <summary>CompleteAccess, then dispose both references; no-op if !IsCreated.</summary>
        public void Dispose()
        {
            if (!IsCreated) return;
            CompleteAccess(); Exclusive.Dispose(); Readers.Dispose();
        }
    }
}
