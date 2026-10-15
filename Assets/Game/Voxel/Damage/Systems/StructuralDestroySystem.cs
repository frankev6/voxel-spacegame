using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.6 final destruction pass; ADR-0117 applier, every machine. It was the last step of ShipDeathSystem, which only
    /// the authority runs; its destroy list comes from the resolver, split and dissolve on the authority, and from exact writes on guests.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ShipDeathSystem))]
    [UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct StructuralDestroySystem : ISystem
    {
        StructuralDestroyJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(true); job.Groups = state.GetBufferLookup<LinkedEntityGroup>();
            job.Storage = state.GetEntityStorageInfoLookup();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<GameplayEndCommandBufferSystem.Singleton>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Grids.Update(ref state); job.Groups.Update(ref state); job.Storage.Update(ref state);
            var data = SystemAPI.GetSingleton<DamageStreams>().Data;
            job.Destroy = data->DestroyList; job.Despawns = data->DissolveDespawns; job.Results = data->GridResults;
            job.Ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var dependency = JobHandle.CombineDependencies(state.Dependency, data->DeathHandle, data->SplitHandle);
            dependency = JobHandle.CombineDependencies(dependency, data->ResolverHandle, data->ExactHandle);
            state.Dependency = job.Schedule(dependency);
            data->DestroyHandle = state.Dependency;
        }
    }
}
