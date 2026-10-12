using System;
using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Simulation.Weapons
{
    /// <summary>Implements SPEC-21 weapon contract: ProjectileStore.</summary>
    public struct ProjectileStore : IComponentData
    {
        public NativeList<ProjectileRow> Rows;           // fire order; compacted stably after each step
        public Game.Data.Streams.GuardedHandles Access;  // main thread only (SPEC-02 §4.12)
        public NativeReference<ProjectileStoreStats> Stats;
        public NativeReference<uint> AppliedShiftSequence;   // last OriginShiftRecord.Sequence applied to Rows (read by SPEC-63 to correct a pending shift)
        public static ProjectileStore Create(int capacity) => throw new NotImplementedException();   // Allocator.Persistent
        public void Dispose() => throw new NotImplementedException();                                // Access.Dispose() (completes) first
        public JobHandle Acquire(JobHandle dependsOn) => Access.Acquire(dependsOn);           // ProjectileStepSystem, WeaponFireSystem
        public void Release(JobHandle lastJob) => Access.Release(lastJob);
        public JobHandle BeginRead(JobHandle dependsOn) => Access.BeginRead(dependsOn);       // read-only jobs
        public void EndRead(JobHandle readerJob) => Access.EndRead(readerJob);
        public void CompleteAccess() => Access.CompleteAccess();   // never in FixedStepSimulationSystemGroup
        /// Inside a single-threaded job: if record.Sequence != applied → (StarSystemChanged == 1 or Sequence − applied > 1 (a rebase was missed)) ? clear all rows : Position += Delta for every row; applied = Sequence. Idempotent.
        public static void ApplyPendingShift(ref NativeList<ProjectileRow> rows, in Game.Core.Components.OriginShiftRecord record, ref NativeReference<uint> applied) => throw new NotImplementedException();
    }
}
