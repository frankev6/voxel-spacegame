using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Spatial
{
    /// <summary>SPEC-23 §5.2: deterministic XZ buckets; large bodies remain separately queryable.</summary>
    [BurstCompile]
    public struct ProxyBucketJob : IJob
    {
        [ReadOnly] public NativeList<SpatialProxy> Proxies;
        public NativeList<int> Large;
        public NativeList<int> EntryIndices;
        public NativeHashMap<long, int2> Cells;
        public NativeReference<SpatialHashState> State;
        public uint Tick;
        public float CellSize;
        struct Entry : System.IComparable<Entry>
        {
            public long Key;
            public int Proxy;
            public int CompareTo(Entry other)
            { int order = Key.CompareTo(other.Key); return order != 0 ? order : Proxy.CompareTo(other.Proxy); }
        }
        public void Execute()
        {
            Large.Clear(); EntryIndices.Clear(); Cells.Clear();
            var state = State.Value;
            state.CellSize = CellSize; state.LargeProxyRadius = CellSize * 4;
            using var entries = new NativeList<Entry>(Allocator.Temp);
            for (int i = 0; i < Proxies.Length; i++)
            {
                var proxy = Proxies[i];
                if (proxy.Radius > state.LargeProxyRadius) { Large.Add(i); continue; }
                int2 lo = (int2)math.floor((proxy.Center.xz - proxy.Radius) / CellSize);
                int2 hi = (int2)math.floor((proxy.Center.xz + proxy.Radius) / CellSize);
                for (int x = lo.x; x <= hi.x; x++)
                    for (int z = lo.y; z <= hi.y; z++)
                        entries.Add(new Entry { Key = SpatialHash.CellKey(new int2(x, z)), Proxy = i });
            }
            entries.AsArray().Sort();
            if (Cells.Capacity < entries.Length) Cells.Capacity = entries.Length;
            int start = 0;
            while (start < entries.Length)
            {
                long key = entries[start].Key;
                int end = start;
                while (end < entries.Length && entries[end].Key == key)
                { EntryIndices.Add(entries[end].Proxy); end++; }
                Cells.Add(key, new int2(start, end - start));
                start = end;
            }
            state.BuiltTick = Tick; state.ProxyCount = Proxies.Length; state.LargeCount = Large.Length;
            state.EntryCount = EntryIndices.Length; state.CellCount = Cells.Count;
            State.Value = state;
        }
    }
}
