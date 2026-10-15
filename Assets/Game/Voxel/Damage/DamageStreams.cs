using Game.Data.Combat;
using Game.Data.Streams;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage
{
    /// <summary>Singleton created by DamageStreamsSystem.OnCreate. All methods: main thread, inside OnUpdate. Implements SPEC-13 §5.1.</summary>
    public unsafe struct DamageStreams : IComponentData
    {
        [Unity.Collections.LowLevel.Unsafe.NativeDisableUnsafePtrRestriction] internal DamageStreamsData* Data;

        // DamageEvent channel (SPEC-02 §4.12). Producers (SPEC-13, 21, 22, 24 and any later spec): Damage.BeginWrite(ref state, ProducerKind, n)
        // → schedule the writer job → Damage.EndWrite(handle). The single consumer is DamageResolveSystem.
        public Game.Data.Streams.SimEventChannel<DamageEvent> Damage;
        // ExplosionRequest channel. Producers: SPEC-13 (reactors, core breaches), 21, 24. The single consumer is SPEC-21 ExplosionSystem,
        // through ScheduleDrainExplosions.
        public Game.Data.Streams.SimEventChannel<ExplosionRequest> Explosions;
        // Request channels for state this module owns (ADR-0016). Consumers: ImpactEventSystem (Impulses), DamageResolveSystem (Restores).
        public Game.Data.Streams.SimEventChannel<ImpulseRequest> Impulses;
        public Game.Data.Streams.SimEventChannel<CellRestoreRequest> Restores;

        // The ExplosionRequest consumer: Explosions.BeginRead, SimEventGather, stable sort by (Source.Index, Producer, Sequence, gather
        // position), Explosions.EndRead. Requests registered after this call are drained next tick.
        public JobHandle ScheduleDrainExplosions(ref SystemState state, NativeList<ExplosionRequest> output)
        {
            var set = Explosions.BeginRead(ref state);
            var cleared = new ClearExplosionsJob { Output = output }.Schedule(state.Dependency);
            var gathered = SimEventGather.Schedule(set, output, cleared);
            Explosions.EndRead(gathered);
            state.Dependency = new SortExplosionsJob { Output = output }.Schedule(gathered);
            return state.Dependency;
        }

        // Same-tick CellRemoved readers (run after DamageResolveSystem, before GameplayEndCommandBufferSystem):
        public CellRemovedView BeginRemovedRead(ref SystemState state)
        {
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, Data->RemovedWriteHandle);
            return new CellRemovedView { Cells = Data->Removed.AsDeferredJobArray(), Grids = Data->RemovedGrids.AsDeferredJobArray() };
        }  // combines the resolver's write handle
        public void EndRemovedRead(JobHandle readerJob) => Data->RemovedReaderHandles.Add(readerJob);
        // Same-tick CellRestored readers (SPEC-12 mass): same rules as BeginRemovedRead.
        public CellRestoredView BeginRestoredRead(ref SystemState state)
        {
            state.Dependency = JobHandle.CombineDependencies(state.Dependency, Data->RemovedWriteHandle);
            return new CellRestoredView { Cells = Data->Restored.AsDeferredJobArray(), Grids = Data->RestoredGrids.AsDeferredJobArray() };
        }
        public void EndRestoredRead(JobHandle readerJob) => Data->RemovedReaderHandles.Add(readerJob);

        // ADR-0117 exact writes (co-op guests): main thread outside the fixed step. Applied in order by ExactCellApplySystem next tick.
        public void QueueExact(NativeArray<ExactCellWrite> writes)
        {
            if (writes.Length == 0) return;
            Data->ExactHandle.Complete(); Data->ExactWrites.AddRange(writes);
        }
        // True while queued exact writes wait for a tick: cell reads (hash checks) must wait too.
        public bool ExactPending { get { Data->ExactHandle.Complete(); return Data->ExactWrites.Length > 0; } }
        // ADR-0117: a co-op guest's copy of the host's hulk transitions. Main thread outside the fixed step; applied next tick.
        public void QueueHulk(HulkTransition transition) { Data->HulkHandle.Complete(); Data->HulkTransitions.Add(transition); }
        // Exact writes dropped because their grid or brick does not exist here (desyncs), since boot.
        public int ExactMissing { get { Data->ExactHandle.Complete(); return Data->ExactMissing; } }

        // SPEC-21 ShipDestroyedBridgeSystem, once per frame (presentation, main thread); completes ShipDeathSystem's last write first.
        public void DrainCoreLost(NativeList<GridCoreLost> output)
        {
            Data->CoreLostWriteHandle.Complete(); output.Clear(); output.AddRange(Data->CoreLost.AsArray()); Data->CoreLost.Clear();
        }

        // ChunkColliderCleanupSystem (§5.9), main thread outside the fixed step: completes SplitSystem's last write, then moves
        // the chunk collider blobs SplitSystem replaced (slot reuse) into output and clears its list.
        public void DrainRetiredChunkBlobs(NativeList<BlobAssetReference<Unity.Physics.Collider>> output)
        {
            JobHandle.CombineDependencies(Data->SplitHandle, Data->RetiredChunkBlobsWriteHandle).Complete();
            output.Clear(); output.AddRange(Data->RetiredChunkBlobs.AsArray()); Data->RetiredChunkBlobs.Clear();
        }

        // Main thread outside fixed simulation only: the completed prior tick is published by a dependent job (Q21).
        public DamageTickStats Totals { get { Data->TickResetHandle.Complete(); return Data->Totals; } }   // running sums since boot
        public DamageTickStats LastTickStats { get { Data->TickResetHandle.Complete(); return Data->Last; } }                  // copied at the start of the next tick

        [BurstCompile] struct ClearExplosionsJob : IJob
        {
            public NativeList<ExplosionRequest> Output;
            public void Execute() => Output.Clear();
        }
        [BurstCompile] struct SortExplosionsJob : IJob
        {
            public NativeList<ExplosionRequest> Output;
            struct Indexed : System.IComparable<Indexed>
            {
                public ExplosionRequest Value; public int GatherIndex;
                public int CompareTo(Indexed b)
                {
                    int c = Value.Source.Index.CompareTo(b.Value.Source.Index);
                    if (c == 0) c = ((byte)Value.Producer).CompareTo((byte)b.Value.Producer);
                    if (c == 0) c = Value.Sequence.CompareTo(b.Value.Sequence);
                    return c != 0 ? c : GatherIndex.CompareTo(b.GatherIndex);
                }
            }
            public void Execute()
            {
                var indexed = new NativeArray<Indexed>(Output.Length, Allocator.Temp);
                for (int i = 0; i < Output.Length; i++) indexed[i] = new Indexed { Value = Output[i], GatherIndex = i };
                indexed.Sort();
                for (int i = 0; i < Output.Length; i++) Output[i] = indexed[i].Value;
                indexed.Dispose();
            }
        }
    }
}
