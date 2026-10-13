using Unity.Collections;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.6/§4.7 reusable persistent route buffers; allocation/lifetime belongs to TRV-004.</summary>
    public sealed class RouteScratch : System.IDisposable
    {
        public NativeArray<byte> SystemFlags;
        public NativeArray<byte> GateOpen;
        internal NativeArray<double> Dist = default;
        internal NativeArray<int> Hops = default, PrevLane = default, PrevSystem = default;
        internal NativeArray<byte> Settled = default;
        internal NativeList<RouteHeapEntry> Heap = default;
        internal NativeList<int> PathLanes = default;
        internal NativeReference<double> TotalLy = default;
        internal NativeReference<byte> Found = default;
        public void EnsureCapacity(int systems, int gates, int lanes) => throw new System.NotImplementedException("TRV-004");
        public void Dispose() => throw new System.NotImplementedException("TRV-004");
    }
}
