using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    public static class GridColliderStats
    {
        /// Main thread, outside the fixed step: completes the cache Access handle, returns a copy.
        public static ColliderPipelineStats Gather(EntityManager em) => throw new System.NotImplementedException("PHY-015");
        public static void ResetCounters(EntityManager em) => throw new System.NotImplementedException("PHY-015");   // tests and harness
    }
}
