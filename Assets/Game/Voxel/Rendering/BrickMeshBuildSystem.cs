using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.Voxel.Rendering
{
    /// <summary>One snapshot-only mesh batch in flight, outside fixed simulation (SPEC-11 §5.4/5.10).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VoxelRebuildGroup))]
    [UpdateBefore(typeof(BrickColliderBuildSystem))]
    public partial struct BrickMeshBuildSystem : ISystem
    {
        ComponentLookup<Brick> bricks;
        ComponentLookup<VoxelGrid> grids;
        ComponentLookup<BrickLodDesired> lods;
        ComponentLookup<Dormant> dormant;
        BufferLookup<BrickSlot> slots;
        BufferLookup<BrickCells> cells;
        BufferLookup<BrickSparseCells> sparse;
        BufferLookup<PaletteEntry> palettes;
        BufferLookup<BlockInstance> instances;
        static readonly ProfilerMarker Dequeue = new ProfilerMarker(ProfilerCategory.Scripts, "BrickMeshBuildSystem.Dequeue");
        static readonly ProfilerMarker Snapshot = new ProfilerMarker(ProfilerCategory.Scripts, "BrickMeshBuildSystem.Snapshot");
        static readonly ProfilerMarker Build = new ProfilerMarker(ProfilerCategory.Scripts, "BrickMeshBuildSystem.Schedule");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<VoxelRenderConfig>();
            state.RequireForUpdate<VoxelRenderRuntime>(); state.RequireForUpdate<BrickMeshQueue>(); state.RequireForUpdate<BrickMeshBatch>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<OrientationTablesRef>(); state.RequireForUpdate<DeferredDisposalQueue>();
            bricks = state.GetComponentLookup<Brick>(true); grids = state.GetComponentLookup<VoxelGrid>(true);
            lods = state.GetComponentLookup<BrickLodDesired>(true); dormant = state.GetComponentLookup<Dormant>(true);
            slots = state.GetBufferLookup<BrickSlot>(true); cells = state.GetBufferLookup<BrickCells>(true);
            sparse = state.GetBufferLookup<BrickSparseCells>(true); palettes = state.GetBufferLookup<PaletteEntry>(true);
            instances = state.GetBufferLookup<BlockInstance>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var runtime = SystemAPI.GetSingleton<VoxelRenderRuntime>();
            ref var batch = ref SystemAPI.GetSingletonRW<BrickMeshBatch>().ValueRW;
            if (runtime.IsReady == 0 || batch.InFlight != 0) return;
            var queue = SystemAPI.GetSingleton<BrickMeshQueue>(); queue.CompleteAccess();
            if (queue.Count == 0) return;
            // Snapshot gathering is the only live-cell reader. Complete earlier ECS writers here, outside fixed simulation.
            state.CompleteDependency();
            bricks.Update(ref state); grids.Update(ref state); lods.Update(ref state); dormant.Update(ref state);
            slots.Update(ref state); cells.Update(ref state); sparse.Update(ref state); palettes.Update(ref state); instances.Update(ref state);
            var config = SystemAPI.GetSingleton<VoxelRenderConfig>();
            batch.EnsureCapacity(config.MeshBatchMax); batch.Work.Clear();
            int count = math.clamp((int)(config.MeshApplyBudgetMs * 1000 / math.max(1, batch.AvgApplyMicros)), config.MeshBatchMin, config.MeshBatchMax);
            using (Dequeue.Auto())
                while (batch.Work.Length < count && queue.TryDequeue(out var request))
                {
                    if (!bricks.HasComponent(request.Brick) || !grids.HasComponent(request.Grid)) continue;
                    byte lod = lods.HasComponent(request.Brick) ? lods[request.Brick].Lod : (byte)0;
                    if (dormant.HasComponent(request.Grid) && dormant.IsComponentEnabled(request.Grid)) lod = 1;
                    batch.Work.Add(new BrickMeshWork { Brick = request.Brick, Grid = request.Grid, Lod = lod });
                }
            if (batch.Work.Length == 0) return;
            using (Snapshot.Auto())
                new BrickSnapshotJob
                {
                    Bricks = bricks, Grids = grids, Slots = slots, Cells = cells, Sparse = sparse, Palettes = palettes, Instances = instances,
                    Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value, Orientations = SystemAPI.GetSingleton<OrientationTablesRef>().Value,
                    Work = batch.Work.AsArray(), Snapshots = batch.Snapshots, Props = batch.Props
                }.Schedule(batch.Work.Length, 1, state.Dependency).Complete();
            using (Build.Auto())
                batch.Handle = new BrickMeshJob { Snapshots = batch.Snapshots, Shapes = runtime.Shapes, Work = batch.Work.AsArray(), Results = batch.Results }
                    .Schedule(batch.Work.Length, 1); // Intentionally independent: no ECS lookups or live cell storage.
            batch.InFlight = 1; batch.ScheduledFrame = SystemAPI.GetSingleton<DeferredDisposalQueue>().Frame;
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        {
            // GridSpawnSystem owns/disposes the batch. Complete our independent handle if its singleton is still alive.
            if (SystemAPI.TryGetSingleton<BrickMeshBatch>(out var batch)) batch.Handle.Complete();
        }
    }
}
