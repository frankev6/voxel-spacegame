using Unity.Entities;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>SPEC-12 §5.14: main-thread snapshots and cumulative-counter reset under cache ownership.</summary>
    public static class GridColliderStats
    {
        /// Main thread, outside the fixed step: completes the cache Access handle, returns a copy.
        public static ColliderPipelineStats Gather(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ColliderBuildCache>());
            if (query.IsEmptyIgnoreFilter) return default;
            var cache = query.GetSingleton<ColliderBuildCache>(); cache.CompleteAccess(); return cache.C.Stats.Value;
        }
        public static void ResetCounters(EntityManager em)
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<ColliderBuildCache>());
            if (query.IsEmptyIgnoreFilter) return;
            var cache = query.GetSingleton<ColliderBuildCache>(); cache.CompleteAccess(); var stats = cache.C.Stats.Value;
            stats.Installs = stats.LateInstalls = stats.DroppedRequests = stats.Batches = stats.ForcedBatchCompletes = 0;
            cache.C.Stats.Value = stats;
        }
    }
}
