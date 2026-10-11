using Unity.Burst;
using Unity.Jobs;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Transfers completed batch ownership under the cache guard (SPEC-12 §5.8/§5.9).</summary>
    [BurstCompile]
    internal struct ColliderFinalizeJob : IJob
    {
        public ColliderCacheContainers Cache;
        public ColliderBatchContainers Batch;
        public void Execute() => ColliderPipeline.Finalize(ref Cache, ref Batch);
    }
}
