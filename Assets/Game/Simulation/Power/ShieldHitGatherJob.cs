using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.5: stable hit ordering and one linear grid-range pass after SimEventGather.</summary>
    [BurstCompile]
    internal struct ShieldHitGatherJob : IJob
    {
        public NativeList<ShieldHit> Hits;
        public NativeParallelHashMap<Entity, int2> Ranges;
        struct Ordered : System.IComparable<Ordered>
        {
            public ShieldHit Hit;
            public int GatherIndex;
            public int CompareTo(Ordered other)
            {
                int c = Hit.Grid.Index.CompareTo(other.Hit.Grid.Index);
                if (c == 0) c = Hit.Source.Index.CompareTo(other.Hit.Source.Index);
                if (c == 0) c = ((byte)Hit.Producer).CompareTo((byte)other.Hit.Producer);
                if (c == 0) c = Hit.Sequence.CompareTo(other.Hit.Sequence);
                return c != 0 ? c : GatherIndex.CompareTo(other.GatherIndex);
            }
        }
        public void Execute()
        {
            var ordered = new NativeArray<Ordered>(Hits.Length, Allocator.Temp);
            for (int i = 0; i < Hits.Length; i++) ordered[i] = new Ordered { Hit = Hits[i], GatherIndex = i };
            ordered.Sort();
            Ranges.Clear();
            if (Ranges.Capacity < Hits.Length) Ranges.Capacity = Hits.Length;
            for (int i = 0; i < Hits.Length; i++) Hits[i] = ordered[i].Hit;
            ordered.Dispose();
            int start = 0;
            while (start < Hits.Length)
            {
                Entity grid = Hits[start].Grid; int end = start + 1;
                while (end < Hits.Length && Hits[end].Grid == grid) end++;
                Ranges.TryAdd(grid, new int2(start, end - start)); start = end;
            }
        }
    }
}
