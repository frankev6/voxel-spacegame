using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    internal struct OrderedDamage : System.IComparable<OrderedDamage>
    {
        public DamageEvent Event;
        public int GatherPosition;
        public int CompareTo(OrderedDamage other)
        {
            int c = Event.Grid.Index.CompareTo(other.Event.Grid.Index);
            if (c == 0) c = (Event.Source == Entity.Null ? -1 : Event.Source.Index).CompareTo(other.Event.Source == Entity.Null ? -1 : other.Event.Source.Index);
            if (c == 0) c = ((byte)Event.Producer).CompareTo((byte)other.Event.Producer);
            if (c == 0) c = Event.Sequence.CompareTo(other.Event.Sequence);
            return c == 0 ? GatherPosition.CompareTo(other.GatherPosition) : c;
        }
    }
    [BurstCompile] internal unsafe struct GatherDamageJob : IJob
    {
        [ReadOnly] public NativeList<DamageEvent> Events;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<DamageImmune> Immune;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        public NativeList<OrderedDamage> Ordered;
        public NativeReference<byte> Warnings;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;
        public void Execute()
        {
            for (int i = 0; i < Events.Length; i++)
            {
                var e = Events[i]; Data->Current.EventsIn++;
                bool finite = math.isfinite(e.Amount) && math.isfinite(e.Radius) && math.isfinite(e.Penetration) &&
                    math.all(math.isfinite(e.LocalPoint)) && math.all(math.isfinite(e.LocalDir)) && math.all(math.isfinite(e.WorldImpulse));
                if (!finite && (Warnings.Value & 2) == 0) { Warnings.Value |= 2; DamageWarnings.BadEvent(); }
                if (!finite || e.Amount <= 0 || !Grids.HasComponent(e.Grid) || Immune.HasComponent(e.Grid) ||
                    (Spawning.HasComponent(e.Grid) && Spawning.IsComponentEnabled(e.Grid)))
                { Data->Current.EventsDropped++; continue; }
                Ordered.Add(new OrderedDamage { Event = e, GatherPosition = i });
            }
        }
    }
}
