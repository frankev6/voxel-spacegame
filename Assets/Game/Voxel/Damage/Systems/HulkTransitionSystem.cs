using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Streams;
using Game.Voxel.Damage.Jobs;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.PresentationStreams;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.6 core-loss consequences; ADR-0117 applier, every machine. ShipDeathSystem (authority) decides core losses and
    /// queues them in the same tick; a co-op guest queues the host's through <see cref="DamageStreams.QueueHulk"/>.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(ShipDeathSystem)), UpdateBefore(typeof(StructuralDestroySystem))]
    [UpdateBefore(typeof(GridMassUpdateSystem)), UpdateBefore(typeof(BrickDirtyScheduleSystem))]
    public unsafe partial struct HulkTransitionSystem : ISystem
    {
        HulkTransitionJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            job.Grids = state.GetComponentLookup<VoxelGrid>(); job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Transforms = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<DamageStreams>(); state.RequireForUpdate<DamageConfig>();
            state.RequireForUpdate<SfxEventQueue>(); state.RequireForUpdate<GameplayEndCommandBufferSystem.Singleton>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            var streams = SystemAPI.GetSingleton<DamageStreams>(); var data = streams.Data;
            job.Grids.Update(ref state); job.Hulks.Update(ref state); job.Transforms.Update(ref state);
            job.Transitions = data->HulkTransitions; job.Config = SystemAPI.GetSingleton<DamageConfig>();
            job.Explosions = streams.Explosions.BeginWrite(ref state, ProducerKind.Explosion, 1);
            job.Ecb = SystemAPI.GetSingleton<GameplayEndCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var sfx = SystemAPI.GetSingleton<SfxEventQueue>(); job.Sfx = sfx.Queue.AsParallelWriter();
            // Scheduled every tick (an empty list is a no-op), so the host never waits on this tick's damage jobs here.
            var dependency = JobHandle.CombineDependencies(state.Dependency, data->DeathHandle, data->HulkHandle);
            state.Dependency = job.Schedule(sfx.Access.BeginRead(dependency));
            sfx.Access.EndRead(state.Dependency); streams.Explosions.EndWrite(state.Dependency);
            data->HulkHandle = state.Dependency;
        }
    }
}
