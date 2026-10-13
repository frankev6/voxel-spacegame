using Game.Core.Components;
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
    // TODO(DMG-013): despawn dissolve and final death presentation; full required tests.
    /// <summary>SPEC-13 §5.6: core-loss transition and the final, deduplicated structural destruction pass.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(SplitSystem))]
    [UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct ShipDeathSystem : ISystem
    {
        ShipDeathJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(); job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Ids = state.GetComponentLookup<PersistentId>(true); job.Groups = state.GetBufferLookup<LinkedEntityGroup>();
            job.Storage = state.GetEntityStorageInfoLookup();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate<GameplayEndCommandBufferSystem.Singleton>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Hulks.Update(ref state); job.Ids.Update(ref state); job.Groups.Update(ref state); job.Storage.Update(ref state);
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data;
            job.Destroy = data->DestroyList; job.Results = data->GridResults; job.CoreLost = data->CoreLost;
            job.Config = SystemAPI.GetSingleton<DamageConfig>(); job.Tick = SystemAPI.GetSingleton<SimClock>().Tick;
            job.Explosions = streams.Explosions.BeginWrite(ref state, ProducerKind.Explosion, 1);
            job.Ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            state.Dependency = job.Schedule(JobHandle.CombineDependencies(state.Dependency, data->SplitHandle, data->ResolverHandle));
            streams.Explosions.EndWrite(state.Dependency); data->DeathHandle = state.Dependency; data->CoreLostWriteHandle = state.Dependency;
        }
    }
}
