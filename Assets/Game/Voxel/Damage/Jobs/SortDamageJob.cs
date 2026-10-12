using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Game.Voxel.Damage.Jobs
{
    [BurstCompile] internal struct SortDamageJob : IJob
    {
        public NativeList<OrderedDamage> Ordered;
        public NativeList<DamageEvent> Events;
        [ReadOnly] public NativeList<PendingDetach> Pending;
        public NativeList<PendingDetach> Detaches;
        struct DetachOrder : System.Collections.Generic.IComparer<PendingDetach>
        {
            public int Compare(PendingDetach a, PendingDetach b)
            {
                int c = a.Grid.Index.CompareTo(b.Grid.Index);
                if (c == 0) c = a.IslandOrdinal.CompareTo(b.IslandOrdinal);
                return c == 0 ? a.SlotIndex.CompareTo(b.SlotIndex) : c;
            }
        }
        public void Execute()
        {
            Ordered.Sort(); Events.Clear(); foreach (var e in Ordered) Events.Add(e.Event);
            Detaches.AddRange(Pending.AsArray()); Detaches.Sort(new DetachOrder());
        }
    }
}
