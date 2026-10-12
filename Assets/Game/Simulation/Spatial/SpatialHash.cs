using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Simulation.Spatial
{
    /// <summary>Implements SPEC-23 §4.1: SpatialHash.</summary>
    public struct SpatialHash : IComponentData
    {
        public NativeList<SpatialProxy> Proxies;         // sorted by Entity.Index
        public NativeList<int> Large;                    // proxy indices with Radius > LargeProxyRadius, ascending
        public NativeHashMap<long, int2> Cells;          // cell key → (start, count) in EntryIndices (lookup only, never iterated)
        public NativeList<int> EntryIndices;             // proxy indices grouped by cell, ascending within a cell
        public NativeReference<SpatialHashState> State;
        public Game.Data.Streams.GuardedHandles Access;  // main thread only (SPEC-02 §4.12): builder = Acquire/Release, readers = BeginRead/EndRead

        public static SpatialHash Create(float cellSize) => throw new System.NotImplementedException();   // Allocator.Persistent; capacities: proxies 512, entries 4,096
        public void Dispose() => throw new System.NotImplementedException();                               // Access.Dispose() (completes) first

        public static long CellKey(int2 cell) => ((long)cell.x << 32) ^ (uint)cell.y;
        public int2 CellOf(float3 p) => throw new System.NotImplementedException(); // TODO(AI-002): coordinate queries.  // main-thread convenience; jobs pass CellSize

        // Job access (§5.1)
        public JobHandle BeginRead(JobHandle dependsOn) => Access.BeginRead(dependsOn);
        public void EndRead(JobHandle readerJob) => Access.EndRead(readerJob);
        public void CompleteAccess() => Access.CompleteAccess();   // never inside FixedStepSimulationSystemGroup

        // Queries (Burst, usable inside jobs; read-only)
        public int QuerySegment(float3 p0, float3 p1, float inflate, ref FixedList512Bytes<int> output) => throw new System.NotImplementedException();
        public int QuerySphere(float3 center, float radius, ref FixedList512Bytes<int> output) => throw new System.NotImplementedException();
        public SpatialProxy Get(int index) => Proxies[index];
        public int FindIndex(Entity e) => throw new System.NotImplementedException();                  // binary search on Entity.Index (and Version); -1 if absent
    }
}
