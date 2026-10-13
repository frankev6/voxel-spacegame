using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Ai;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Simulation.Spatial
{
    /// <summary>SPEC-23 §5.1–5.2: sole spatial writer; readers join the owner's guarded job chain.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderFirst = true)]
    public partial struct SpatialHashBuildSystem : ISystem
    {
        static readonly ProfilerMarker GatherMarker = new ProfilerMarker("SpatialHashBuildSystem.Gather");
        static readonly ProfilerMarker SortMarker = new ProfilerMarker("SpatialHashBuildSystem.Sort");
        static readonly ProfilerMarker BucketMarker = new ProfilerMarker("SpatialHashBuildSystem.Bucket");
        EntityQuery query;
        Entity singleton;
        SpatialHash hash;
        ProxyGatherJob gather;
        public void OnCreate(ref SystemState state)
        {
            query = new EntityQueryBuilder(Allocator.Temp).WithAll<LocalTransform>()
                .WithAny<VoxelGrid, SpatialBody>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).Build(ref state);
            hash = SpatialHash.Create(128);
            singleton = state.EntityManager.CreateEntity(typeof(SpatialHash));
            state.EntityManager.SetComponentData(singleton, hash);
            gather = new ProxyGatherJob
            {
                Entities = state.GetEntityTypeHandle(), Transforms = state.GetComponentTypeHandle<LocalTransform>(true),
                Grids = state.GetComponentLookup<VoxelGrid>(true), Bodies = state.GetComponentLookup<SpatialBody>(true),
                Spawning = state.GetComponentLookup<GridSpawning>(true), Shields = state.GetComponentLookup<ShieldState>(true),
                Velocities = state.GetComponentLookup<PhysicsVelocity>(true), Allegiances = state.GetComponentLookup<Allegiance>(true),
                Hulks = state.GetComponentLookup<Hulk>(true), Dormancy = state.GetComponentLookup<Dormant>(true),
                Players = state.GetComponentLookup<PlayerShip>(true), Brains = state.GetComponentLookup<AiBrain>(true)
            };
            state.RequireForUpdate<GameplayActive>();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            gather.Entities.Update(ref state); gather.Transforms.Update(ref state);
            gather.Grids.Update(ref state); gather.Bodies.Update(ref state); gather.Spawning.Update(ref state);
            gather.Shields.Update(ref state); gather.Velocities.Update(ref state); gather.Allegiances.Update(ref state);
            gather.Hulks.Update(ref state); gather.Dormancy.Update(ref state); gather.Players.Update(ref state); gather.Brains.Update(ref state);
            var stream = new NativeStream(math.max(1, query.CalculateChunkCountWithoutFiltering()), Allocator.TempJob);
            gather.Output = stream.AsWriter();
            var dependency = hash.Access.Acquire(state.Dependency);
            using (GatherMarker.Auto()) dependency = gather.ScheduleParallel(query, dependency);
            using (SortMarker.Auto()) dependency = new ProxySortJob { Input = stream, Proxies = hash.Proxies }.Schedule(dependency);
            float cellSize = SystemAPI.TryGetSingleton<AiConfig>(out var config) ? config.HashCellSize : 128;
            uint tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0;
            using (BucketMarker.Auto()) dependency = new ProxyBucketJob
            {
                Proxies = hash.Proxies, Large = hash.Large, EntryIndices = hash.EntryIndices,
                Cells = hash.Cells, State = hash.State, CellSize = cellSize, Tick = tick
            }.Schedule(dependency);
            dependency = stream.Dispose(dependency);
            hash.Access.Release(dependency); state.Dependency = dependency;
        }
        public void OnDestroy(ref SystemState state)
        {
            hash.Dispose();
            if (state.EntityManager.Exists(singleton)) state.EntityManager.DestroyEntity(singleton);
        }
    }
}
