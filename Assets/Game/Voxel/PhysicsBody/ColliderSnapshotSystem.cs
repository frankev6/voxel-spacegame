using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Copies tick-final solid masks into owned collider updates without a fixed-step sync (SPEC-12 §5.7).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(BrickDirtyScheduleSystem))]
    [UpdateBefore(typeof(GameplayEndCommandBufferSystem))]
    public partial struct ColliderSnapshotSystem : ISystem
    {
        static readonly ProfilerMarker Collect = new ProfilerMarker(ProfilerCategory.Scripts, "ColliderSnapshotSystem.Collect");
        BufferLookup<BrickSolidMask> masks;
        ComponentLookup<Brick> bricks;
        EntityStorageInfoLookup entities;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            masks = state.GetBufferLookup<BrickSolidMask>(true);
            bricks = state.GetComponentLookup<Brick>(true);
            entities = state.GetEntityStorageInfoLookup();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate<PhysicsVoxelConfig>();
            state.RequireForUpdate<ColliderDirtyQueue>(); state.RequireForUpdate<ColliderBuildCache>(); state.RequireForUpdate<ColliderBatchQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = Collect.Auto();
            masks.Update(ref state); bricks.Update(ref state); entities.Update(ref state);
            var queue = SystemAPI.GetSingleton<ColliderDirtyQueue>(); var cache = SystemAPI.GetSingleton<ColliderBuildCache>();
            var config = SystemAPI.GetSingleton<PhysicsVoxelConfig>();
            var dependency = cache.Acquire(queue.Acquire(state.Dependency));
            var handle = new ColliderCollectJob
            {
                Requests = queue.Requests, Cache = cache.C, Masks = masks, Bricks = bricks, Entities = entities,
                Tick = SystemAPI.GetSingleton<SimClock>().Tick, Delay = config.InstallDelayTicks, Bucket = config.InstallBucketTicks
            }.Schedule(dependency);
            queue.Release(handle); cache.Release(handle); state.Dependency = handle;
        }
    }
}
