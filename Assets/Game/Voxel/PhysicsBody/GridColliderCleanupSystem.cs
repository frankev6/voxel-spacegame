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
            using (Retire.Auto())
            {
                // The queue mirrors the previous presentation tick until DeferredDisposalSystem runs later this frame.
                // Stamp this local view from the current clock so catch-up ticks cannot shorten the physics lifetime (Q21).
                if (SystemAPI.TryGetSingleton<SimClock>(out var clock)) deferred.Tick = clock.Tick;
                for (int i = 0; i < cache.C.Retired.Length; i++) DeferredDisposal.RetireBlob(ref deferred, cache.C.Retired[i]);
                ColliderPipeline.ClearTransferredRetired(ref cache.C);
            }
        }
    }
}
