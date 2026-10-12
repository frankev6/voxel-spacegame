using Game.Core.Components;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Entities;
using Unity.Profiling;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.10: transfers dead-grid collider ownership to deferred disposal, including outside gameplay.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(DeferredDisposalSystem))]
    public partial struct GridColliderCleanupSystem : ISystem
    {
        static readonly ProfilerMarker Sweep = new ProfilerMarker(ProfilerCategory.Scripts, "GridColliderCleanupSystem.Sweep");
        static readonly ProfilerMarker Retire = new ProfilerMarker(ProfilerCategory.Scripts, "GridColliderCleanupSystem.Retire");
        EntityStorageInfoLookup entities;

        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            entities = state.GetEntityStorageInfoLookup();
            state.RequireForUpdate<ColliderBuildCache>(); state.RequireForUpdate<ColliderBatchQueue>();
            state.RequireForUpdate<ColliderDirtyQueue>(); state.RequireForUpdate<DeferredDisposalQueue>();
        }

        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            entities.Update(ref state);
            var cache = SystemAPI.GetSingleton<ColliderBuildCache>(); var batch = SystemAPI.GetSingleton<ColliderBatchQueue>();
            var queue = SystemAPI.GetSingleton<ColliderDirtyQueue>();
            cache.CompleteAccess();
            bool active = SystemAPI.HasSingleton<GameplayActive>();
            var deferred = SystemAPI.GetSingleton<DeferredDisposalQueue>();
            // Cleanup must also work after scene/config teardown; the default retains the two-frame safety bound (Q20).
            int maxAge = SystemAPI.TryGetSingleton<PhysicsVoxelConfig>(out var config) ? config.MaxBatchAgeFrames : PhysicsVoxelConfig.Default.MaxBatchAgeFrames;
            var status = batch.State.Value;
            if (status.InFlight != 0 && (!active || deferred.Frame - status.ScheduledFrame >= maxAge))
            {
                bool completed = status.Handle.IsCompleted;
                status.Handle.Complete();
                if (!completed) { var stats = cache.C.Stats.Value; stats.ForcedBatchCompletes++; cache.C.Stats.Value = stats; }
                ColliderPipeline.Finalize(ref cache, ref batch); batch.State.Value = default;
            }
            using (Sweep.Auto())
                for (int i = cache.C.Grids.Length - 1; i >= 0; i--)
                    if (!active || !entities.Exists(cache.C.Grids[i].Root)) ColliderPipeline.RemoveRecord(ref cache.C, i, batch.B);
            if (!active) { queue.CompleteAccess(); queue.Requests.Clear(); }
            // Stamp both retirement paths from this tick, not the prior presentation frame (Q21).
            if (SystemAPI.TryGetSingleton<SimClock>(out var clock)) deferred.Tick = clock.Tick;
            foreach (var (pending, entity) in SystemAPI.Query<RefRW<GridUnadoptedCollider>>().WithNone<VoxelGrid>().WithEntityAccess())
            {
                var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
                if (pending.ValueRO.Value.IsCreated) DeferredDisposal.RetireBlob(ref deferred, pending.ValueRO.Value);
                pending.ValueRW.Value = default; ecb.RemoveComponent<GridUnadoptedCollider>(entity);
            }
            using (Retire.Auto())
            {
                for (int i = 0; i < cache.C.Retired.Length; i++) DeferredDisposal.RetireBlob(ref deferred, cache.C.Retired[i]);
                ColliderPipeline.ClearTransferredRetired(ref cache.C);
            }
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            foreach (var pending in SystemAPI.Query<RefRW<GridUnadoptedCollider>>())
            {
                var blob = pending.ValueRO.Value;
                if (blob.IsCreated) blob.Dispose();
                pending.ValueRW.Value = default;
            }
        }

    }
}
