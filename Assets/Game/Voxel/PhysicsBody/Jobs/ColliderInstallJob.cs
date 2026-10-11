using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;
using Unity.Physics;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Installs the newest due prefix and transfers exactly one compound into installed ownership (SPEC-12 §5.9).</summary>
    [BurstCompile]
    internal struct ColliderInstallJob : IJob
    {
        static readonly ProfilerMarker ExecuteMarker = new ProfilerMarker(ProfilerCategory.Scripts, "ColliderInstallJob.Execute");
        public ColliderCacheContainers Cache;
        public ComponentLookup<PhysicsCollider> Colliders;
        public ComponentLookup<GridColliderState> States;
        [ReadOnly] public EntityStorageInfoLookup Entities;
        public uint Tick;
        public void Execute()
        {
            using var marker = ExecuteMarker.Auto();
            for (int r = 0; r < Cache.Grids.Length; r++)
            {
                var record = Cache.Grids[r]; int prefix = 0;
                for (int i = 0; i < record.Updates.Length; i++)
                {
                    int index = record.Updates[i]; var update = Cache.Updates[index];
                    bool due = update.InstallTick == 0 || update.InstallTick <= Tick;
                    if (update.State == UpdateState.Built && due) { prefix++; continue; }
                    if (due && update.InstallTick != 0 && update.State != UpdateState.Built && update.LateCounted == 0)
                    {
                        update.LateCounted = 1; Cache.Updates[index] = update;
                        var stats = Cache.Stats.Value; stats.LateInstalls++; Cache.Stats.Value = stats;
                    }
                    break;
                }
                if (prefix == 0) continue;
                int newest = record.Updates[prefix - 1]; var chosen = Cache.Updates[newest];
                if (Entities.Exists(record.Root) && Colliders.HasComponent(record.Root))
                {
                    var state = States[record.Root];
                    if (chosen.Compound.IsCreated)
                    {
                        Colliders[record.Root] = new PhysicsCollider { Value = chosen.Compound };
                        var stats = Cache.Stats.Value;
                        stats.InstalledBytes += ColliderPipeline.Bytes(chosen.Compound) - ColliderPipeline.Bytes(record.Installed);
                        stats.PendingCompoundBytes -= ColliderPipeline.Bytes(chosen.Compound); stats.Installs++; Cache.Stats.Value = stats;
                        ColliderPipeline.Retire(ref Cache, record.Installed);
                        record.Installed = chosen.Compound; Cache.Grids[r] = record;
                        chosen.Compound = default; Cache.Updates[newest] = chosen;
                        state.Phase = GridColliderPhase.Ready; state.InstalledTick = Tick;
                        state.ChildCount = chosen.ChildCount; state.BoxCount = chosen.BoxCount;
                    }
                    else state.ChildCount = 0; // Empty updates keep the installed shape; pending-prefix blobs still retire below (Q19).
                    States[record.Root] = state;
                }
                // FreeUpdate removes each index from the record and retires any compound not transferred above.
                for (int i = 0; i < prefix; i++) ColliderPipeline.FreeUpdate(ref Cache, Cache.Grids[r].Updates[0]);
            }
        }
    }
}
