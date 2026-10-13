using System;
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
        bool disposed;

        public void EnsureCapacity(int systems, int gates, int lanes)
        {
            if (disposed) throw new ObjectDisposedException(nameof(RouteScratch));
            if (systems < 0) throw new ArgumentOutOfRangeException(nameof(systems));
            if (gates < 0) throw new ArgumentOutOfRangeException(nameof(gates));
            if (lanes < 0) throw new ArgumentOutOfRangeException(nameof(lanes));
            // Each adjacency can relax at most once, plus the source: the job never grows its heap.
            int heapCapacity = checked(2 * lanes + 1);
            Grow(ref SystemFlags, systems); Grow(ref GateOpen, gates);
            Grow(ref Dist, systems); Grow(ref Hops, systems);
            Grow(ref PrevLane, systems); Grow(ref PrevSystem, systems); Grow(ref Settled, systems);
            if (!Heap.IsCreated) Heap = new NativeList<RouteHeapEntry>(heapCapacity, Allocator.Persistent);
            else if (Heap.Capacity < heapCapacity) Heap.Capacity = heapCapacity;
            int pathCapacity = Math.Max(1, systems);
            if (!PathLanes.IsCreated) PathLanes = new NativeList<int>(pathCapacity, Allocator.Persistent);
            else if (PathLanes.Capacity < pathCapacity) PathLanes.Capacity = pathCapacity;
            if (!TotalLy.IsCreated) TotalLy = new NativeReference<double>(Allocator.Persistent);
            if (!Found.IsCreated) Found = new NativeReference<byte>(Allocator.Persistent);
        }

        static void Grow<T>(ref NativeArray<T> buffer, int count) where T : unmanaged
        {
            count = Math.Max(1, count);
            if (buffer.IsCreated && buffer.Length >= count) return;
            var replacement = new NativeArray<T>(count, Allocator.Persistent);
            if (buffer.IsCreated) { NativeArray<T>.Copy(buffer, replacement, buffer.Length); buffer.Dispose(); }
            buffer = replacement;
        }

        public void Dispose()
        {
            if (disposed) return;
            if (SystemFlags.IsCreated) SystemFlags.Dispose();
            if (GateOpen.IsCreated) GateOpen.Dispose();
            if (Dist.IsCreated) Dist.Dispose();
            if (Hops.IsCreated) Hops.Dispose();
            if (PrevLane.IsCreated) PrevLane.Dispose();
            if (PrevSystem.IsCreated) PrevSystem.Dispose();
            if (Settled.IsCreated) Settled.Dispose();
            if (Heap.IsCreated) Heap.Dispose();
            if (PathLanes.IsCreated) PathLanes.Dispose();
            if (TotalLy.IsCreated) TotalLy.Dispose();
            if (Found.IsCreated) Found.Dispose();
            SystemFlags = default; GateOpen = default; Dist = default;
            Hops = default; PrevLane = default; PrevSystem = default; Settled = default;
            Heap = default; PathLanes = default; TotalLy = default; Found = default;
            disposed = true;
        }
    }
}
