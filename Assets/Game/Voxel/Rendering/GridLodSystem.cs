using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Voxel.Rendering
{
    /// <summary>SPEC-11 §5.7: the sole runtime writer of dormancy and desired brick LOD, after world transforms.</summary>
    [BurstCompile, UpdateInGroup(typeof(WorldGroup), OrderLast = true)]
    public partial struct GridLodSystem : ISystem
    {
        EntityQuery grids, bricks, changed;
        ComponentLookup<Dormant> dormantWrite, dormantRead;
        ComponentLookup<BrickLodDesired> desires;
        EntityTypeHandle entities;
        ComponentTypeHandle<Brick> brickType;
        ComponentTypeHandle<BrickLodDesired> desiredType;
        ComponentTypeHandle<BrickRenderState> stateType;
        static readonly ProfilerMarker Grids = new ProfilerMarker(ProfilerCategory.Scripts, "GridLodSystem.Grids");
        static readonly ProfilerMarker Bricks = new ProfilerMarker(ProfilerCategory.Scripts, "GridLodSystem.Bricks");
        static readonly ProfilerMarker Enqueue = new ProfilerMarker(ProfilerCategory.Scripts, "GridLodSystem.Enqueue");
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<VoxelViewer>();
            state.RequireForUpdate<VoxelRenderConfig>(); state.RequireForUpdate<BrickMeshQueue>();
            grids = SystemAPI.QueryBuilder().WithAll<VoxelGrid, LocalToWorld, Dormant>()
                .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build();
            bricks = SystemAPI.QueryBuilder().WithAll<Brick, LocalToWorld, BrickRenderState, BrickLodDesired>().Build();
            changed = SystemAPI.QueryBuilder().WithAll<Brick, BrickRenderState, BrickLodDesired>().Build();
            changed.SetChangedVersionFilter(ComponentType.ReadOnly<BrickLodDesired>());
            changed.AddChangedVersionFilter(ComponentType.ReadOnly<BrickRenderState>());
            dormantWrite = state.GetComponentLookup<Dormant>(); dormantRead = state.GetComponentLookup<Dormant>(true);
            desires = state.GetComponentLookup<BrickLodDesired>(); entities = state.GetEntityTypeHandle();
            brickType = state.GetComponentTypeHandle<Brick>(true); desiredType = state.GetComponentTypeHandle<BrickLodDesired>(true);
            stateType = state.GetComponentTypeHandle<BrickRenderState>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            dormantWrite.Update(ref state); dormantRead.Update(ref state); desires.Update(ref state);
            entities.Update(ref state); brickType.Update(ref state); desiredType.Update(ref state); stateType.Update(ref state);
            var viewer = SystemAPI.GetSingleton<VoxelViewer>(); var config = SystemAPI.GetSingleton<VoxelRenderConfig>();
            using (Grids.Auto())
                state.Dependency = new GridDormancyJob { Viewer = viewer, Distance = config.DormantDistance,
                    Hysteresis = config.LodHysteresis, Dormancy = dormantWrite }.ScheduleParallel(grids, state.Dependency);
            using (Bricks.Auto())
                state.Dependency = new BrickLodJob { Viewer = viewer, Distance = config.Lod1Distance,
                    Hysteresis = config.LodHysteresis, Dormancy = dormantRead, Desires = desires }.ScheduleParallel(bricks, state.Dependency);
            using (Enqueue.Auto())
            {
                var queue = SystemAPI.GetSingleton<BrickMeshQueue>();
                var handle = new LodEnqueueJob { Entities = entities, Bricks = brickType, Desires = desiredType, States = stateType,
                    Damage = queue.Damage, Neighbour = queue.Neighbour, Spawn = queue.Spawn, Lod = queue.Lod,
                    Heads = queue.Heads, Pending = queue.Pending }.Schedule(changed, queue.Acquire(state.Dependency));
                queue.Release(handle); state.Dependency = handle;
            }
        }
    }
}
