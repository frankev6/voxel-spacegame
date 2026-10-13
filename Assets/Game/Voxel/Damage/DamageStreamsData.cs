using Game.Data.Streams;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Game.Voxel.Damage
{
    internal unsafe struct CellMask { public fixed ulong Words[64]; }
    internal struct DamageGridRange { public Entity Grid; public int EventStart, EventCount, DetachStart, DetachCount, RestoreStart, RestoreCount; }
    internal struct DamageGridResult { public Entity Grid; public int TouchedBrickStart, TouchedBrickCount; public byte CoreLost; public float3 CoreWorldPos; }
    internal struct TouchedBrick { public Entity Grid; public int SlotIndex; public Entity Brick; }
    internal struct IslandRecord { public Entity Grid; public int MaskStart, MaskCount, CellCount; public int3 CellMin, CellMax; public byte HasFunctional; public int Ordinal; }
    internal struct IslandMask { public int SlotIndex; public CellMask Mask; }
    internal struct PendingDetach { public Entity Grid; public int SlotIndex; public CellMask Mask; public CellRemovedReason Reason; public int IslandOrdinal; }
    internal struct ChunkPoolSlot { public Entity Body; public uint EndTick; public byte Live; }

    /// <summary>SPEC-13 sections 4.4/4.8, Q21. Main thread owns handles. Jobs receive container copies, never look up
    /// swappable list fields through this pointer. Only the ordered stats jobs dereference Current/Last.
    /// Each later phase stores its final job in its named handle and depends on TickResetHandle.
    /// Resolver also chains RemovedWriteHandle/readers; Split chains DetachNextWritable and ResolverHandle.</summary>
    internal unsafe struct DamageStreamsData
    {
        public NativeList<CellRemoved> Removed;
        public NativeList<CellRestored> Restored;
        public NativeList<GridRemovedRange> RemovedGrids, RestoredGrids;
        public UnsafeList<JobHandle> RemovedReaderHandles;
        public JobHandle RemovedWriteHandle, CoreLostWriteHandle, RetiredChunkBlobsWriteHandle;
        public JobHandle ResolverHandle, IntegrityHandle, SplitHandle, DeathHandle, ImpactHandle, TickResetHandle, DetachNextWritable;
        public NativeList<GridCoreLost> CoreLost;
        public NativeList<BlobAssetReference<Collider>> RetiredChunkBlobs;
        public DamageTickStats Current, Last;
        public int IntegrityNodesVisited; // Last completed hybrid race batch; internal harness/test evidence.
        public NativeList<DamageGridRange> GridRanges;
        public NativeList<DamageEvent> SortedEvents;
        public NativeList<DamageGridResult> GridResults;
        public NativeList<TouchedBrick> TouchedBricks;
        public NativeList<Entity> DestroyList;
        public NativeList<IslandRecord> Islands;
        public NativeList<IslandMask> IslandMasks;
        public NativeList<PendingDetach> PendingDetachCurrent, PendingDetachNext;
        public NativeList<ChunkPoolSlot> ChunkPool;
        public NativeHashMap<ulong, uint> RamPairs;

        public static DamageStreamsData* Create(int maxLiveChunks)
        {
            var data = (DamageStreamsData*)UnsafeUtility.MallocTracked(UnsafeUtility.SizeOf<DamageStreamsData>(), UnsafeUtility.AlignOf<DamageStreamsData>(), Allocator.Persistent, 0);
            *data = default;
            data->Removed = new NativeList<CellRemoved>(4096, Allocator.Persistent);
            data->Restored = new NativeList<CellRestored>(4096, Allocator.Persistent);
            data->RemovedGrids = new NativeList<GridRemovedRange>(256, Allocator.Persistent);
            data->RestoredGrids = new NativeList<GridRemovedRange>(256, Allocator.Persistent);
            data->RemovedReaderHandles = new UnsafeList<JobHandle>(8, Allocator.Persistent);
            data->CoreLost = new NativeList<GridCoreLost>(64, Allocator.Persistent);
            data->RetiredChunkBlobs = new NativeList<BlobAssetReference<Collider>>(64, Allocator.Persistent);
            data->GridRanges = new NativeList<DamageGridRange>(256, Allocator.Persistent);
            data->SortedEvents = new NativeList<DamageEvent>(4096, Allocator.Persistent);
            data->GridResults = new NativeList<DamageGridResult>(256, Allocator.Persistent);
            data->TouchedBricks = new NativeList<TouchedBrick>(256, Allocator.Persistent);
            data->DestroyList = new NativeList<Entity>(256, Allocator.Persistent);
            data->Islands = new NativeList<IslandRecord>(64, Allocator.Persistent);
            data->IslandMasks = new NativeList<IslandMask>(64, Allocator.Persistent);
            data->PendingDetachCurrent = new NativeList<PendingDetach>(64, Allocator.Persistent);
            data->PendingDetachNext = new NativeList<PendingDetach>(64, Allocator.Persistent);
            data->ChunkPool = new NativeList<ChunkPoolSlot>(math.max(1, maxLiveChunks), Allocator.Persistent);
            data->RamPairs = new NativeHashMap<ulong, uint>(1024, Allocator.Persistent);
            return data;
        }
        public JobHandle AllJobs(JobHandle dependency)
        {
            dependency = JobHandle.CombineDependencies(dependency, ResolverHandle, IntegrityHandle);
            dependency = JobHandle.CombineDependencies(dependency, SplitHandle, DeathHandle);
            dependency = JobHandle.CombineDependencies(dependency, ImpactHandle, TickResetHandle);
            dependency = JobHandle.CombineDependencies(dependency, DetachNextWritable, RemovedWriteHandle);
            dependency = JobHandle.CombineDependencies(dependency, CoreLostWriteHandle, RetiredChunkBlobsWriteHandle);
            for (int i = 0; i < RemovedReaderHandles.Length; i++) dependency = JobHandle.CombineDependencies(dependency, RemovedReaderHandles[i]);
            return dependency;
        }
        public void Dispose()
        {
            AllJobs(default).Complete();
            Removed.Dispose(); Restored.Dispose(); RemovedGrids.Dispose(); RestoredGrids.Dispose(); RemovedReaderHandles.Dispose();
            CoreLost.Dispose();
            foreach (var item in RetiredChunkBlobs) { var blob = item; if (blob.IsCreated) blob.Dispose(); }
            RetiredChunkBlobs.Dispose(); GridRanges.Dispose(); SortedEvents.Dispose(); GridResults.Dispose();
            TouchedBricks.Dispose(); DestroyList.Dispose(); Islands.Dispose(); IslandMasks.Dispose();
            PendingDetachCurrent.Dispose(); PendingDetachNext.Dispose(); ChunkPool.Dispose(); RamPairs.Dispose();
        }
    }
}
