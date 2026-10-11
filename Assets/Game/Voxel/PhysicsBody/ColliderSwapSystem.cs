using Game.Core.Components;
using Game.Core.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Profiling;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Schedules completed-batch handover and due installs without waiting in the fixed step (SPEC-12 §5.9).</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateAfter(typeof(AbilityActivationGroup))]
    public partial struct ColliderSwapSystem : ISystem
    {
        static readonly ProfilerMarker Install = new ProfilerMarker(ProfilerCategory.Scripts, "ColliderSwapSystem.Install");
        ComponentLookup<PhysicsCollider> colliders;
        ComponentLookup<GridColliderState> states;
        EntityStorageInfoLookup entities;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            colliders = state.GetComponentLookup<PhysicsCollider>(); states = state.GetComponentLookup<GridColliderState>();
            entities = state.GetEntityStorageInfoLookup();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>();
            state.RequireForUpdate<ColliderDirtyQueue>(); state.RequireForUpdate<ColliderBuildCache>(); state.RequireForUpdate<ColliderBatchQueue>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            using var marker = Install.Auto();
            colliders.Update(ref state); states.Update(ref state); entities.Update(ref state);
            var cache = SystemAPI.GetSingleton<ColliderBuildCache>(); var batch = SystemAPI.GetSingleton<ColliderBatchQueue>();
            var status = batch.State.Value;
            if (status.InFlight != 0 && status.Handle.IsCompleted)
            {
                var finalize = new ColliderFinalizeJob { Cache = cache.C, Batch = batch.B }
                    .Schedule(cache.Acquire(JobHandle.CombineDependencies(state.Dependency, status.Handle)));
                status.InFlight = 0;
                // Handover still owns batch memory until its job finishes, including during owner teardown.
                status.Handle = finalize; batch.State.Value = status; cache.Release(finalize);
            }
            var install = new ColliderInstallJob
            {
                Cache = cache.C, Colliders = colliders, States = states, Entities = entities, Tick = SystemAPI.GetSingleton<SimClock>().Tick
            }.Schedule(cache.Acquire(state.Dependency));
            cache.Release(install); state.Dependency = install;
        }
    }
}
