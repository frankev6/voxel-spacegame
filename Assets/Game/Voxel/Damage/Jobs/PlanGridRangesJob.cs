using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Voxel.Damage.Jobs
{
    internal struct ResolvedGrid
    {
        public DamageGridResult Result;
        public int Destroyed, Detached, Instances, Healed, Placed, RestoreDropped;
        public byte RadiusClamped;
    }
    [BurstCompile] internal struct PlanGridRangesJob : IJob
    {
        [ReadOnly] public NativeList<DamageEvent> Events;
        [ReadOnly] public NativeList<PendingDetach> Detaches;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public NativeList<RestoreSpan> Restores;
        public NativeList<DamageGridRange> Ranges;
        public NativeList<ResolvedGrid> Results;
        public void Execute()
        {
            Ranges.Clear(); int e = 0, d = 0, r = 0;
            while (e < Events.Length || d < Detaches.Length || r < Restores.Length)
            {
                var root = e < Events.Length ? Events[e].Grid : d < Detaches.Length ? Detaches[d].Grid : Restores[r].Request.Grid;
                if (d < Detaches.Length && Detaches[d].Grid.Index < root.Index) root = Detaches[d].Grid;
                if (r < Restores.Length && Restores[r].Request.Grid.Index < root.Index) root = Restores[r].Request.Grid;
                var range = new DamageGridRange { Grid = root, EventStart = e, DetachStart = d, RestoreStart = r };
                while (e < Events.Length && Events[e].Grid == root) e++;
                while (d < Detaches.Length && Detaches[d].Grid == root) d++;
                while (r < Restores.Length && Restores[r].Request.Grid == root) r++;
                range.RestoreCount = r - range.RestoreStart;
                range.EventCount = e - range.EventStart; range.DetachCount = d - range.DetachStart;
                if (Grids.HasComponent(root)) Ranges.Add(range);
            }
            Results.Resize(Ranges.Length, NativeArrayOptions.ClearMemory);
        }
    }
}
