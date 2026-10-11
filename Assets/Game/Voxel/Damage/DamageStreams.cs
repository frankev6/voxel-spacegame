using Game.Data.Combat;
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
        public JobHandle ScheduleDrainExplosions(ref SystemState state, NativeList<ExplosionRequest> output) { throw new System.NotImplementedException("DMG-002"); }

        // Same-tick CellRemoved readers (run after DamageResolveSystem, before GameplayEndCommandBufferSystem):
        public CellRemovedView BeginRemovedRead(ref SystemState state) { throw new System.NotImplementedException("DMG-002"); }  // combines the resolver's write handle
        public void EndRemovedRead(JobHandle readerJob) { throw new System.NotImplementedException("DMG-002"); }
        // Same-tick CellRestored readers (SPEC-12 mass): same rules as BeginRemovedRead.
        public CellRestoredView BeginRestoredRead(ref SystemState state) { throw new System.NotImplementedException("DMG-002"); }
        public void EndRestoredRead(JobHandle readerJob) { throw new System.NotImplementedException("DMG-002"); }

        // SPEC-21 ShipDestroyedBridgeSystem, once per frame (presentation, main thread); completes ShipDeathSystem's last write first.
        public void DrainCoreLost(NativeList<GridCoreLost> output) { throw new System.NotImplementedException("DMG-002"); }

        // ChunkColliderCleanupSystem (§5.9), main thread outside the fixed step: completes SplitSystem's last write, then moves
        // the chunk collider blobs SplitSystem replaced (slot reuse) into output and clears its list.
        public void DrainRetiredChunkBlobs(NativeList<BlobAssetReference<Unity.Physics.Collider>> output) { throw new System.NotImplementedException("DMG-002"); }

        public DamageTickStats LastTickStats => throw new System.NotImplementedException("DMG-002");                  // copied at the start of the next tick
    }
}
