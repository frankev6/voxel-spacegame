using Game.Galaxy.Data;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §5.6 synchronous deterministic Dijkstra search.</summary>
    [Unity.Burst.BurstCompile]
    internal struct RouteSearchJob : Unity.Jobs.IJob
    {
        [ReadOnly] public NativeArray<StarSystemDef> Systems;
        [ReadOnly] public NativeArray<LaneDef> Lanes;
        [ReadOnly] public NativeArray<int> Adjacency;
        [ReadOnly] public NativeArray<byte> SystemFlags, GateOpen;
        public int Source, Target;
        public byte IgnoreLocks;
        public NativeArray<double> Dist;
        public NativeArray<int> Hops, PrevLane, PrevSystem;
        public NativeArray<byte> Settled;
        public NativeList<RouteHeapEntry> Heap;
        public NativeList<int> PathLanes;
        public NativeReference<double> TotalLy;
        public NativeReference<byte> Found;
        public void Execute()
        {
            Heap.Clear(); PathLanes.Clear(); Found.Value = 0; TotalLy.Value = 0;
            for (int i = 0; i < Systems.Length; i++)
            {
                Dist[i] = double.PositiveInfinity; Hops[i] = int.MaxValue;
                PrevLane[i] = -1; PrevSystem[i] = -1; Settled[i] = 0;
            }
            if ((uint)Source >= Systems.Length || (uint)Target >= Systems.Length ||
                (SystemFlags[Source] & 1) == 0 || (SystemFlags[Target] & 1) == 0) return;
            Dist[Source] = 0; Hops[Source] = 0;
            Push(new RouteHeapEntry { Dist = 0, Hops = 0, Id = Systems[Source].Id.Value, Index = Source });
            while (Heap.Length > 0)
            {
                var entry = Pop(); int u = entry.Index;
                if (Settled[u] != 0 || entry.Dist > Dist[u]) continue;
                Settled[u] = 1;
                if (u == Target) break;
                var system = Systems[u];
                for (int slot = 0; slot < system.LaneCount; slot++)
                {
                    int laneIndex = Adjacency[system.FirstAdjacency + slot]; var lane = Lanes[laneIndex];
                    uint neighbour = lane.A.Equals(system.Id) ? lane.B.Value : lane.A.Value;
                    int v = FindIndex(neighbour);
                    if (v < 0 || Settled[v] != 0 || (SystemFlags[v] & 1) == 0) continue;
                    bool visible = ((SystemFlags[u] & SystemFlags[v] & 1) != 0 && (lane.Flags & LaneFlags.HiddenUntilKnown) == 0) ||
                        (SystemFlags[u] & SystemFlags[v] & 2) != 0;
                    if (!visible || (IgnoreLocks == 0 && lane.GateIndex != 0 && GateOpen[lane.GateIndex - 1] == 0)) continue;
                    double nd = Dist[u] + lane.LengthLy; int nh = Hops[u] + 1;
                    bool tied = math.abs(nd - Dist[v]) <= 1e-9;
                    if (!(nd < Dist[v] - 1e-9 || (tied && (nh < Hops[v] ||
                        (nh == Hops[v] && (PrevSystem[v] < 0 || system.Id.Value < Systems[PrevSystem[v]].Id.Value))))) continue;
                    Dist[v] = nd; Hops[v] = nh; PrevLane[v] = laneIndex; PrevSystem[v] = u;
                    Push(new RouteHeapEntry { Dist = nd, Hops = nh, Id = Systems[v].Id.Value, Index = v });
                }
            }
            Found.Value = Settled[Target];
            if (Found.Value == 0) return;
            TotalLy.Value = Dist[Target];
            for (int v = Target; v != Source; v = PrevSystem[v]) PathLanes.AddNoResize(PrevLane[v]);
            for (int i = 0, j = PathLanes.Length - 1; i < j; i++, j--)
            { int lane = PathLanes[i]; PathLanes[i] = PathLanes[j]; PathLanes[j] = lane; }
        }

        int FindIndex(uint id)
        {
            int lo = 0, hi = Systems.Length - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2; uint value = Systems[mid].Id.Value;
                if (value == id) return mid;
                if (value < id) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }
        static bool Before(RouteHeapEntry a, RouteHeapEntry b) => a.Dist < b.Dist ||
            (a.Dist == b.Dist && (a.Hops < b.Hops || (a.Hops == b.Hops && a.Id < b.Id)));

        void Push(RouteHeapEntry entry)
        {
            int index = Heap.Length; Heap.AddNoResize(entry);
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!Before(entry, Heap[parent])) break;
                Heap[index] = Heap[parent]; index = parent;
            }
            Heap[index] = entry;
        }
        RouteHeapEntry Pop()
        {
            var result = Heap[0]; var tail = Heap[Heap.Length - 1]; Heap.ResizeUninitialized(Heap.Length - 1);
            if (Heap.Length == 0) return result;
            int index = 0;
            while (2 * index + 1 < Heap.Length)
            {
                int child = 2 * index + 1;
                if (child + 1 < Heap.Length && Before(Heap[child + 1], Heap[child])) child++;
                if (!Before(Heap[child], tail)) break;
                Heap[index] = Heap[child]; index = child;
            }
            Heap[index] = tail;
            return result;
        }
    }
}
