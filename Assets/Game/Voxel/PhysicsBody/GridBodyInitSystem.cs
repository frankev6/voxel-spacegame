using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.6: asynchronously gathers roots and records their initial bodies into the gameplay-end ECB.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateAfter(typeof(ColliderSwapSystem))]
    public partial struct GridBodyInitSystem : ISystem
    {
        static readonly ProfilerMarker Init = new ProfilerMarker(ProfilerCategory.Scripts, "GridBodyInitSystem.Init");
        EntityQuery init, rebuild;
        GridBodyInitJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            init = new EntityQueryBuilder(Allocator.Temp).WithAll<GridBodyInit, VoxelGrid, GridSource, GridMass, GridColliderState, LocalTransform>()
                .WithAll<BrickSlot>().WithDisabled<GridSpawning>().Build(ref state);
            rebuild = new EntityQueryBuilder(Allocator.Temp).WithAll<GridRebuilt, VoxelGrid, GridSource, GridMass, GridColliderState, LocalTransform>()
                .WithAll<BrickSlot>().WithDisabled<GridSpawning>().Build(ref state);
            job.Grids = state.GetComponentLookup<VoxelGrid>(true); job.Sources = state.GetComponentLookup<GridSource>(true);
            job.Masses = state.GetComponentLookup<GridMass>(true); job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            job.Colliders = state.GetComponentLookup<PhysicsCollider>(true); job.Damping = state.GetComponentLookup<PhysicsDamping>(true);
            job.Bricks = state.GetComponentLookup<Brick>(true); job.Slots = state.GetBufferLookup<BrickSlot>(true);
            job.Inits = state.GetComponentLookup<GridBodyInit>(); job.Rebuilt = state.GetComponentLookup<GridRebuilt>();
            job.Recompute = state.GetComponentLookup<GridMassRecompute>(); job.States = state.GetComponentLookup<GridColliderState>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<PhysicsVoxelConfig>();
            state.RequireForUpdate<BlockDefsRef>(); state.RequireForUpdate<GameplayEndCommandBufferSystem.Singleton>();
            state.RequireForUpdate<ColliderDirtyQueue>(); state.RequireForUpdate<ColliderBuildCache>(); state.RequireForUpdate<ColliderBatchQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            // IsEmpty synchronizes enableable writers; the asynchronous gather must apply those filters instead (Q22).
            if (init.IsEmptyIgnoreFilter && rebuild.IsEmptyIgnoreFilter) return;
            using var marker = Init.Auto();
            job.Grids.Update(ref state); job.Sources.Update(ref state); job.Masses.Update(ref state); job.Transforms.Update(ref state);
            job.Colliders.Update(ref state); job.Damping.Update(ref state); job.Bricks.Update(ref state); job.Slots.Update(ref state);
            job.Inits.Update(ref state); job.Rebuilt.Update(ref state); job.Recompute.Update(ref state); job.States.Update(ref state);
            var queue = SystemAPI.GetSingleton<ColliderDirtyQueue>(); var cache = SystemAPI.GetSingleton<ColliderBuildCache>();
            var batch = SystemAPI.GetSingleton<ColliderBatchQueue>();
            job.InitRoots = init.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var initHandle);
            job.RebuildRoots = rebuild.ToEntityListAsync(state.WorldUpdateAllocator, state.Dependency, out var rebuildHandle);
            job.Cache = cache.C; job.Requests = queue.Requests;
            // Immutable batch descriptors are safe to read alongside construction; results remain exclusive to their jobs.
            // Cache ownership orders this reader before the next finalization/plan can resize these lists (Q23).
            job.BatchBricks = batch.B.Bricks; job.BatchAssemblies = batch.B.Assemblies;
            job.Config = SystemAPI.GetSingleton<PhysicsVoxelConfig>(); job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            job.Ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var handle = job.Schedule(cache.Acquire(queue.Acquire(JobHandle.CombineDependencies(state.Dependency, initHandle, rebuildHandle))));
            queue.Release(handle); cache.Release(handle); state.Dependency = handle;
        }
    }
}
