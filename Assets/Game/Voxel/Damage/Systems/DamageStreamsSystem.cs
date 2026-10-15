using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;

[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Voxel.Damage.DamageEvent>))]
[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Voxel.Damage.ExplosionRequest>))]
[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Voxel.Damage.ImpulseRequest>))]
[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Voxel.Damage.CellRestoreRequest>))]

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 section 5.1: world-lifetime damage memory and tick boundaries; Q21 schedules stats reset safely.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderFirst = true)]
    [UpdateBefore(typeof(GridMassUpdateSystem))]
    public unsafe partial struct DamageStreamsSystem : ISystem, ISystemStartStop
    {
        DamageStreams owned;
        static readonly ProfilerMarker Tick = new ProfilerMarker("DamageStreamsSystem.BeginTick");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            var config = SystemAPI.TryGetSingleton<DamageConfig>(out var existing) ? existing : DamageConfig.Default;
            if (!SystemAPI.HasSingleton<DamageConfig>()) state.EntityManager.CreateSingleton(config);
            owned = new DamageStreams
            {
                Damage = SimEventChannel<DamageEvent>.Create("damage"), Explosions = SimEventChannel<ExplosionRequest>.Create("explosions"),
                Impulses = SimEventChannel<ImpulseRequest>.Create("impulses"), Restores = SimEventChannel<CellRestoreRequest>.Create("restores"),
                Data = DamageStreamsData.Create(config.MaxLiveChunks)
            };
            state.EntityManager.CreateSingleton(owned);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnStartRunning(ref SystemState state) { }
        [BurstCompile] public void OnStopRunning(ref SystemState state)
        {
            owned.Data->AllJobs(state.Dependency).Complete();
            owned.Damage.BeginRead(ref state); owned.Damage.EndRead(state.Dependency);
            owned.Explosions.BeginRead(ref state); owned.Explosions.EndRead(state.Dependency);
            owned.Impulses.BeginRead(ref state); owned.Impulses.EndRead(state.Dependency);
            owned.Restores.BeginRead(ref state); owned.Restores.EndRead(state.Dependency);
            var data = owned.Data;
            data->PendingDetachCurrent.Clear(); data->PendingDetachNext.Clear(); data->CoreLost.Clear();
            data->Removed.Clear(); data->RemovedGrids.Clear(); data->Restored.Clear(); data->RestoredGrids.Clear();
            data->Islands.Clear(); data->IslandMasks.Clear(); data->DestroyList.Clear(); data->GridResults.Clear();
            data->TouchedBricks.Clear(); data->DissolveDespawns.Clear(); data->ExactWrites.Clear(); data->HulkTransitions.Clear();
            data->RamPairs.Clear(); data->Current = default; data->Last = default;
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = Tick.Auto();
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            owned.Damage.BeginTick(tick); owned.Explosions.BeginTick(tick); owned.Impulses.BeginTick(tick); owned.Restores.BeginTick(tick);
            var data = owned.Data;
            var reset = new ResetTickJob { Data = data, Tick = tick, ChunkPool = data->ChunkPool, MaxLiveChunks = SystemAPI.GetSingleton<DamageConfig>().MaxLiveChunks,
                Removed = data->Removed, RemovedGrids = data->RemovedGrids, Restored = data->Restored, RestoredGrids = data->RestoredGrids,
                Touched = data->TouchedBricks, Destroy = data->DestroyList, Dissolved = data->DissolveDespawns, Grids = data->GridResults }.Schedule(data->AllJobs(state.Dependency));
            data->RemovedReaderHandles.Clear();
            data->TickResetHandle = reset;
            var previous = data->PendingDetachCurrent; data->PendingDetachCurrent = data->PendingDetachNext; data->PendingDetachNext = previous;
            data->DetachNextWritable = new ClearListJob { List = data->PendingDetachNext }.Schedule(reset);
            state.Dependency = data->DetachNextWritable;
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            owned.Damage.Dispose(); owned.Explosions.Dispose(); owned.Impulses.Dispose(); owned.Restores.Dispose();
            if (owned.Data == null) return;
            owned.Data->Dispose(); UnsafeUtility.FreeTracked(owned.Data, Allocator.Persistent); owned.Data = null;
        }
        [BurstCompile] struct ClearListJob : IJob
        {
            public NativeList<PendingDetach> List;
            public void Execute() => List.Clear();
        }
        [BurstCompile] struct ResetTickJob : IJob
        {
            [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
            public uint Tick;
            public NativeList<ChunkPoolSlot> ChunkPool;
            public int MaxLiveChunks;
            // ADR-0117: the tick's published lists start empty here, not in the resolver, so every applier (the resolver on the
            // authority, ExactCellApplySystem on co-op guests) appends to the same lists.
            public NativeList<CellRemoved> Removed; public NativeList<CellRestored> Restored;
            public NativeList<GridRemovedRange> RemovedGrids, RestoredGrids;
            public NativeList<TouchedBrick> Touched; public NativeList<Entity> Destroy, Dissolved; public NativeList<DamageGridResult> Grids;
            public void Execute()
            {
                Removed.Clear(); RemovedGrids.Clear(); Restored.Clear(); RestoredGrids.Clear(); Touched.Clear(); Destroy.Clear(); Dissolved.Clear(); Grids.Clear();
                ref var totals = ref Data->Totals; var current = Data->Current;
                totals.CellsDestroyed += current.CellsDestroyed; totals.CellsDetached += current.CellsDetached; totals.InstancesDestroyed += current.InstancesDestroyed;
                totals.DebrisParticles += current.DebrisParticles; totals.CellsPlaced += current.CellsPlaced; totals.GridsResolved += current.GridsResolved;
                Data->Last = Data->Current; Data->Current = new DamageTickStats { Tick = Tick };
                // Config is authored after automatic OnCreate; first gameplay tick sees the boot-configured cap.
                if (ChunkPool.Length == 0) ChunkPool.Resize(Unity.Mathematics.math.max(0, MaxLiveChunks), NativeArrayOptions.ClearMemory);
            }
        }
    }
}
