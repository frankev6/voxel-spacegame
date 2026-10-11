using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Game.Voxel.Model;

namespace Game.Voxel.PhysicsBody
{
    /// <summary>Implements SPEC-12 §4 (physics contract).</summary>
    internal enum UpdateState : byte { Free = 0, Collecting = 1, Pending = 2, Building = 3, Built = 4, Discarded = 5 }

    internal struct UpdateBrick { public int3 Coord; public int MaskSlot; }

    internal struct ColliderUpdate
    {
        public UpdateState State;
        public ColliderPriority Priority;      // most urgent (lowest value) of its requests
        public byte LateCounted;               // 1 after LateInstalls was incremented for it
        public byte Reserved;
        public uint Serial, Seq, CreateOrder;  // Seq: per grid, creation order; CreateOrder: global creation order
        public uint RequestTick;               // tick of its first request
        public uint InstallTick;               // 0 = install when built
        public UnsafeList<UpdateBrick> Bricks; // one entry per coord (newest snapshot wins)
        public int BricksScheduled;            // prefix of Bricks already handed to batches
        public BlobAssetReference<Collider> Compound;   // valid when Built
        public int ChildCount, BoxCount;
    }

    internal struct GridColliderRecord
    {
        public Entity Root; public uint Serial; public float CellSize;
        public GridMotion Motion; public byte IsEditorPreview;
        public UnsafeList<BrickChild> Children;          // latest built blob per brick, sorted (z, y, x)
        public UnsafeList<int> Updates;                  // indices into Updates, not yet installed, Seq order
        public BlobAssetReference<Collider> Installed;   // what PhysicsCollider currently points at
        public uint NextSeq;
    }

    internal struct ColliderCacheContainers
    {
        public NativeHashMap<uint, int> GridIndex;                 // Serial → index in Grids (capacity 256)
        public NativeList<GridColliderRecord> Grids;               // swap-back removal; GridIndex fixed up
        public NativeList<ColliderUpdate> Updates;                 // pool (capacity 256); State Free = unused
        public NativeList<int> FreeUpdates;
        public NativeHashMap<ulong, int> Collecting;               // (Serial << 32 | InstallTick) → update index while Collecting
        public NativeHashMap<CollectKey, int> CollectingBricks;    // (update index, Coord) → index in that update's Bricks
        public NativeList<BrickMaskBits> Masks;                    // snapshot pool (capacity 256 = 128 KB)
        public NativeList<int> FreeMasks;
        public NativeList<BlobAssetReference<Collider>> Retired;   // blobs for DeferredDisposalQueue (moved on the main thread, §5.10)
        public NativeReference<ColliderPipelineStats> Stats;
        public NativeReference<uint> NextCreateOrder;
    }

    internal struct CollectKey : System.IEquatable<CollectKey>
    {
        public int Update;
        public int3 Coord;
        public bool Equals(CollectKey other) => Update == other.Update && math.all(Coord == other.Coord);
        public override bool Equals(object obj) => obj is CollectKey other && Equals(other);
        // Hash-table lookup only; never used for simulation ordering or persisted data.
        public override int GetHashCode() => (int)math.hash(new int4(Coord, Update));
    }

    internal struct ColliderBatchState { public Unity.Jobs.JobHandle Handle; public byte InFlight; public uint ScheduledFrame; }

    internal struct BrickBuildItem { public int Update; public uint Serial; public int3 Coord; public float CellSize; public byte Removed; public byte EditorPreview; }

    internal struct BrickBuildResult { public BlobAssetReference<Collider> Blob; public int BoxCount; public byte Removed; }

    internal struct AssemblyItem { public int Update; public uint Serial; public float CellSize; public int ChildStart, ChildCount; public int ApplyStart, ApplyCount; }

    internal struct AssemblyResult { public BlobAssetReference<Collider> Compound; public int ChildCount, BoxCount; }

    internal struct ColliderBatchContainers
    {
        public NativeList<BrickBuildItem> Bricks;           // capacity 512
        public NativeList<BrickMaskBits> Masks;             // parallel to Bricks
        public NativeList<BrickBuildResult> Results;        // parallel to Bricks
        public NativeList<AssemblyItem> Assemblies;         // capacity 64
        public NativeList<BrickChild> AssemblyChildren;     // copies of record children at schedule time
        public NativeList<int> AssemblyApply;               // indices into Bricks applied, in order, on top of the copied children
        public NativeList<AssemblyResult> AssemblyResults;  // parallel to Assemblies
    }
}
