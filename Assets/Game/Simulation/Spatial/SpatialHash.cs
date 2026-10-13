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

        public static SpatialHash Create(float cellSize)
        {
            if (!math.isfinite(cellSize) || cellSize <= 0 || !math.isfinite(cellSize * 4))
                throw new System.ArgumentOutOfRangeException(nameof(cellSize));
            return new SpatialHash
            {
                Proxies = new NativeList<SpatialProxy>(512, Allocator.Persistent),
                Large = new NativeList<int>(512, Allocator.Persistent),
                Cells = new NativeHashMap<long, int2>(4096, Allocator.Persistent),
                EntryIndices = new NativeList<int>(4096, Allocator.Persistent),
                State = new NativeReference<SpatialHashState>(new SpatialHashState
                    { CellSize = cellSize, LargeProxyRadius = 4 * cellSize }, Allocator.Persistent),
                Access = Game.Data.Streams.GuardedHandles.Create()
            };
        }
        public void Dispose()
        {
            Access.Dispose();
            if (Proxies.IsCreated) Proxies.Dispose();
            if (Large.IsCreated) Large.Dispose();
            if (Cells.IsCreated) Cells.Dispose();
            if (EntryIndices.IsCreated) EntryIndices.Dispose();
            if (State.IsCreated) State.Dispose();
            this = default;
        }

        public static long CellKey(int2 cell) => ((long)cell.x << 32) ^ (uint)cell.y;
        public int2 CellOf(float3 p) => (int2)math.floor(p.xz / State.Value.CellSize);

        // Job access (§5.1)
        public JobHandle BeginRead(JobHandle dependsOn) => Access.BeginRead(dependsOn);
        public void EndRead(JobHandle readerJob) => Access.EndRead(readerJob);
        public void CompleteAccess() => Access.CompleteAccess();   // never inside FixedStepSimulationSystemGroup

        // Queries (Burst, usable inside jobs; read-only)
        public int QuerySegment(float3 p0, float3 p1, float inflate, ref FixedList512Bytes<int> output)
            => AsReadOnly().QuerySegment(p0, p1, inflate, ref output);
        public int QuerySphere(float3 center, float radius, ref FixedList512Bytes<int> output)
            => AsReadOnly().QuerySphere(center, radius, ref output);
        public SpatialProxy Get(int index) => Proxies[index];
        public int FindIndex(Entity e) => AsReadOnly().FindIndex(e);
        /// <summary>Job-safe data view. Handle bookkeeping stays exclusively on the main thread (ADR-0064).</summary>
        public SpatialHashQueryData AsReadOnly() => new SpatialHashQueryData
        { Proxies = Proxies, Large = Large, Cells = Cells, EntryIndices = EntryIndices, State = State };

    }
}
