using Unity.Burst;
using Unity.Jobs;
using Unity.Profiling;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Transfers completed batch ownership under the cache guard (SPEC-12 §5.8/§5.9).</summary>
    [BurstCompile]
    internal struct ColliderFinalizeJob : IJob
    {
        static readonly ProfilerMarker ExecuteMarker = new ProfilerMarker(ProfilerCategory.Scripts, "ColliderFinalizeJob.Execute");
        public ColliderCacheContainers Cache;
        public ColliderBatchContainers Batch;
        public void Execute()
        { using var marker = ExecuteMarker.Auto(); ColliderPipeline.Finalize(ref Cache, ref Batch); }
    }
}
