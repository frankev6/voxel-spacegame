using Game.Core.Components;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Voxel.PresentationStreams;
using Unity.Transforms;
using Unity.Physics;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Voxel.Grid;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.6: core-loss transition and grid dissolving (authority decisions). The final destruction pass is
    /// StructuralDestroySystem's, which every machine runs (ADR-0117).</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(SplitSystem))]
    [UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct ShipDeathSystem : ISystem
    {
        ShipDeathJob job;
        DissolveJob dissolve;
        EntityQuery requests, dissolving;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(true); job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Ids = state.GetComponentLookup<PersistentId>(true); job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            dissolve.Reader = GridCellReader.Create(ref state);
            dissolve.Bricks = state.GetComponentLookup<Brick>(true);
            dissolve.Transforms = state.GetComponentLookup<LocalTransform>(true);
            dissolve.Velocities = state.GetComponentLookup<PhysicsVelocity>(true);
            dissolve.Masses = state.GetComponentLookup<PhysicsMass>(true);
            dissolve.Palettes = state.GetBufferLookup<PaletteEntry>(true);
            dissolve.Instances = state.GetBufferLookup<BlockInstance>(true);
            dissolve.Requests = state.GetComponentLookup<GridDissolveRequest>(true);
            dissolve.Progress = state.GetComponentLookup<GridDissolving>();
            requests = new EntityQueryBuilder(Allocator.Temp).WithAll<GridDissolveRequest>().Build(ref state);
            dissolving = new EntityQueryBuilder(Allocator.Temp).WithAll<GridDissolving, VoxelGrid>().Build(ref state);
            state.RequireForUpdate<FxEventQueue>(); state.RequireForUpdate<SfxEventQueue>(); state.RequireForUpdate<BlockDefsRef>();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate<GameplayEndCommandBufferSystem.Singleton>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Hulks.Update(ref state); job.Ids.Update(ref state); job.Transforms.Update(ref state);
            dissolve.Reader.Update(ref state); dissolve.Bricks.Update(ref state); dissolve.Transforms.Update(ref state);
            dissolve.Velocities.Update(ref state); dissolve.Masses.Update(ref state); dissolve.Palettes.Update(ref state);
            dissolve.Instances.Update(ref state); dissolve.Requests.Update(ref state); dissolve.Progress.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data;
            job.Results = data->GridResults; job.CoreLost = data->CoreLost; job.Transitions = data->HulkTransitions;
            var config = SystemAPI.GetSingleton<DamageConfig>(); job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var fx = SystemAPI.GetSingleton<FxEventQueue>();
            dissolve.RequestEntities = requests.ToEntityListAsync(Allocator.TempJob, out var requestHandle);
            dissolve.DissolvingEntities = dissolving.ToEntityListAsync(Allocator.TempJob, out var dissolveHandle);
            dissolve.Destroy = data->DissolveDespawns;   // StructuralDestroySystem destroys them with the rest
            dissolve.Config = config; dissolve.Tick = job.Tick; dissolve.Ecb = ecb;
            dissolve.Blocks = SystemAPI.GetSingleton<BlockDefsRef>().Value; dissolve.Fx = fx.Queue.AsParallelWriter();
            var dependency = JobHandle.CombineDependencies(state.Dependency, data->SplitHandle, JobHandle.CombineDependencies(data->ResolverHandle, data->HulkHandle));
            dependency = JobHandle.CombineDependencies(dependency, requestHandle, dissolveHandle);
            dependency = dissolve.Schedule(fx.Access.BeginRead(dependency));
            fx.Access.EndRead(dependency);
            dependency = job.Schedule(dependency);
            dependency = dissolve.RequestEntities.Dispose(dependency);
            dependency = dissolve.DissolvingEntities.Dispose(dependency);
            state.Dependency = dependency;
            data->DeathHandle = state.Dependency; data->CoreLostWriteHandle = state.Dependency;
        }
    }
}
