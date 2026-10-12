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
        public int Destroyed, Detached, Instances;
        public byte RadiusClamped;
    }
    [BurstCompile] internal struct PlanGridRangesJob : IJob
    {
        [ReadOnly] public NativeList<DamageEvent> Events;
        [ReadOnly] public NativeList<PendingDetach> Detaches;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        public NativeList<DamageGridRange> Ranges;
        public NativeList<ResolvedGrid> Results;
        public void Execute()
        {
            Ranges.Clear(); int e = 0, d = 0;
            while (e < Events.Length || d < Detaches.Length)
            {
                var root = e < Events.Length && (d == Detaches.Length || Events[e].Grid.Index <= Detaches[d].Grid.Index) ? Events[e].Grid : Detaches[d].Grid;
                var range = new DamageGridRange { Grid = root, EventStart = e, DetachStart = d };
                while (e < Events.Length && Events[e].Grid == root) e++;
                while (d < Detaches.Length && Detaches[d].Grid == root) d++;
                range.EventCount = e - range.EventStart; range.DetachCount = d - range.DetachStart;
                if (Grids.HasComponent(root)) Ranges.Add(range);
            }
            Results.Resize(Ranges.Length, NativeArrayOptions.ClearMemory);
        }
    }
}
