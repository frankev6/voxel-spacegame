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
    public struct ColliderPipelineStats
    {
        public int Grids, CachedBricks, CachedBoxes, PendingUpdates, PendingBricks;
        public long CachedBrickBytes, InstalledBytes, PendingCompoundBytes, RetiredPendingBytes;
        public int LastBatchBricks, LastBatchAssemblies, LastBatchUrgentBricks;
        public int Installs, LateInstalls, DroppedRequests, Batches, ForcedBatchCompletes;   // cumulative since the cache was created or ResetCounters
    }
}
