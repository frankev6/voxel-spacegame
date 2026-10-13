using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.3: ordered inputs, exclusive per-grid resolution, canonical serial publication.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageStreamsSystem)), UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct DamageResolveSystem : ISystem, ISystemStartStop
    {
        DamageAccess access;
        NativeReference<byte> warnings;
        NativeReference<ulong> nextRestoreId;
        NativeList<CellRestoreRequest> incomingRestores;
        NativeList<RestorePending> pendingRestores;
        NativeList<RestoreSpan> restoreSpans;
        NativeList<RestoreHealedInstance> healedInstances;
        static readonly ProfilerMarker Gather = new ProfilerMarker(ProfilerCategory.Scripts, "DamageResolveSystem.Gather");
        static readonly ProfilerMarker Sort = new ProfilerMarker(ProfilerCategory.Scripts, "DamageResolveSystem.Sort");
        static readonly ProfilerMarker Plan = new ProfilerMarker(ProfilerCategory.Scripts, "DamageResolveSystem.Plan");
        static readonly ProfilerMarker Resolve = new ProfilerMarker(ProfilerCategory.Scripts, "DamageResolveSystem.Resolve");
        static readonly ProfilerMarker Flatten = new ProfilerMarker(ProfilerCategory.Scripts, "DamageResolveSystem.Flatten");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            nextRestoreId = new NativeReference<ulong>(Allocator.Persistent);
            incomingRestores = new NativeList<CellRestoreRequest>(Allocator.Persistent);
            pendingRestores = new NativeList<RestorePending>(Allocator.Persistent);
            restoreSpans = new NativeList<RestoreSpan>(Allocator.Persistent);
            healedInstances = new NativeList<RestoreHealedInstance>(Allocator.Persistent);
            warnings = new NativeReference<byte>(Allocator.Persistent); access = DamageAccess.Create(ref state);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<BlockModulesRef>(); state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<SimClock>();
        }
        [BurstCompile] public void OnStartRunning(ref SystemState state) { }
        [BurstCompile] public void OnStopRunning(ref SystemState state)
        {
            state.CompleteDependency(); incomingRestores.Clear(); pendingRestores.Clear(); restoreSpans.Clear(); healedInstances.Clear(); nextRestoreId.Value = 0;
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency(); warnings.Dispose(); nextRestoreId.Dispose(); incomingRestores.Dispose();
            pendingRestores.Dispose(); restoreSpans.Dispose(); healedInstances.Dispose();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            access.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data;
            state.Dependency = data->AllJobs(state.Dependency);
            var restoreSet = streams.Restores.BeginRead(ref state);
            state.Dependency = new ClearRestoresJob { Requests = incomingRestores }.Schedule(state.Dependency);
            state.Dependency = SimEventGather.Schedule(restoreSet, incomingRestores, state.Dependency);
            streams.Restores.EndRead(state.Dependency);
            state.Dependency = new PlanRestoresJob
            {
                Incoming = incomingRestores, Pending = pendingRestores, Spans = restoreSpans, Healed = healedInstances,
                NextId = nextRestoreId, Grids = access.Grids, Spawning = access.Spawning,
                Budget = SystemAPI.GetSingleton<DamageConfig>().MaxRestoreCellsPerTick, Data = data
            }.Schedule(state.Dependency);
            var ordered = new NativeList<OrderedDamage>(Allocator.TempJob);
            var detaches = new NativeList<PendingDetach>(Allocator.TempJob);
            var results = new NativeList<ResolvedGrid>(Allocator.TempJob);
            JobHandle gathered;
            using (Gather.Auto())
            {
                var set = streams.Damage.BeginRead(ref state);
                var clear = new ClearEventsJob { Events = data->SortedEvents }.Schedule(state.Dependency);
                var read = SimEventGather.Schedule(set, data->SortedEvents, clear); streams.Damage.EndRead(read);
                gathered = new GatherDamageJob { Events = data->SortedEvents, Ordered = ordered, Grids = access.Grids,
                    Immune = access.Immune, Spawning = access.Spawning, Warnings = warnings, Data = data }.Schedule(read);
            }
            JobHandle sorted;
            using (Sort.Auto()) sorted = new SortDamageJob { Events = data->SortedEvents, Ordered = ordered,
                Pending = data->PendingDetachCurrent, Detaches = detaches }.Schedule(gathered);
            JobHandle planned;
            using (Plan.Auto()) planned = new PlanGridRangesJob { Events = data->SortedEvents, Detaches = detaches,
                Grids = access.Grids, Ranges = data->GridRanges, Results = results, Restores = restoreSpans }.Schedule(sorted);
            var constructed = NativeStream.ScheduleConstruct(out var removed, data->GridRanges, planned, Allocator.TempJob);
            constructed = JobHandle.CombineDependencies(constructed, NativeStream.ScheduleConstruct(out var touched, data->GridRanges, planned, Allocator.TempJob));
            constructed = JobHandle.CombineDependencies(constructed, NativeStream.ScheduleConstruct(out var destroyed, data->GridRanges, planned, Allocator.TempJob));
            constructed = JobHandle.CombineDependencies(constructed, NativeStream.ScheduleConstruct(out var explosions, data->GridRanges, planned, Allocator.TempJob));
            constructed = JobHandle.CombineDependencies(constructed, NativeStream.ScheduleConstruct(out var restored, data->GridRanges, planned, Allocator.TempJob));
            constructed = JobHandle.CombineDependencies(constructed, NativeStream.ScheduleConstruct(out var healed, data->GridRanges, planned, Allocator.TempJob));
            // Dynamic range count is known only in jobs. Flatten forwards explosions to the existing
            // one-index channel in canonical range order, without a main-thread completion/API change.
            var publish = streams.Explosions.BeginWrite(ref state, ProducerKind.Explosion, 1);
            JobHandle resolved;
            using (Resolve.Auto()) resolved = new ResolveGridJob
            {
                Access = access, Config = SystemAPI.GetSingleton<DamageConfig>(), Tick = SystemAPI.GetSingleton<SimClock>().Tick,
                Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value, Modules = SystemAPI.GetSingleton<BlockModulesRef>().Value,
                Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value, Events = data->SortedEvents,
                Pending = detaches, Ranges = data->GridRanges.AsDeferredJobArray(), Results = results.AsDeferredJobArray(),
                Restores = restoreSpans, PreviouslyHealed = healedInstances, Restored = restored.AsWriter(), HealedInstances = healed.AsWriter(),
                Removed = removed.AsWriter(), Touched = touched.AsWriter(), DestroyList = destroyed.AsWriter(), Explosions = explosions.AsWriter()
            }.Schedule(data->GridRanges, 1, JobHandle.CombineDependencies(constructed, state.Dependency));
            JobHandle flattened;
            using (Flatten.Auto()) flattened = new FlattenRemovedJob
            {
                Results = results, Removed = removed.AsReader(), Touched = touched.AsReader(), DestroyList = destroyed.AsReader(), Explosions = explosions.AsReader(),
                RestoredInput = restored.AsReader(), HealedInput = healed.AsReader(), Healed = healedInstances,
                PublishedExplosions = publish, Cells = data->Removed, Ranges = data->RemovedGrids, Bricks = data->TouchedBricks, Destroy = data->DestroyList,
                Grids = data->GridResults, Restored = data->Restored, RestoredRanges = data->RestoredGrids, Dirty = access.Dirty, Warnings = warnings, Data = data
            }.Schedule(resolved);
            streams.Explosions.EndWrite(flattened); data->RemovedWriteHandle = flattened;
            var cleanup = JobHandle.CombineDependencies(ordered.Dispose(flattened), detaches.Dispose(flattened), results.Dispose(flattened));
            cleanup = JobHandle.CombineDependencies(cleanup, removed.Dispose(flattened), touched.Dispose(flattened));
            cleanup = JobHandle.CombineDependencies(cleanup, destroyed.Dispose(flattened), explosions.Dispose(flattened));
            cleanup = JobHandle.CombineDependencies(cleanup, restored.Dispose(flattened), healed.Dispose(flattened));
            data->ResolverHandle = cleanup; state.Dependency = cleanup;
        }
        [BurstCompile] struct ClearRestoresJob : IJob
        { public NativeList<CellRestoreRequest> Requests; public void Execute() => Requests.Clear(); }
        [BurstCompile] struct ClearEventsJob : IJob
        { public NativeList<DamageEvent> Events; public void Execute() => Events.Clear(); }
    }
}
