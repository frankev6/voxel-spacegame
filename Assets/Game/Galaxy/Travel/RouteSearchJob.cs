using Game.Galaxy.Data;
using Unity.Collections;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.7 route-search job shell; TRV-004 owns the algorithm.</summary>
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
        public void Execute() => throw new System.NotImplementedException("TRV-004");
    }
}
