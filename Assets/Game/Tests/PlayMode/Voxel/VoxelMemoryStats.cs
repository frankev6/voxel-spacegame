using System;
using Game.Data.Streams;
using Game.Voxel.Grid;
using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Physics;

namespace Game.Tests.PlayMode.Voxel
{
    /// <summary>SPEC-10 §8.2 allocated capacity accounting. Shared content, engine/driver heaps and render targets are separate from voxel-owned storage.</summary>
    internal struct VoxelMemoryStats
    {
        public long Chunks, BufferHeap, MeshGpu, MeshNative, MeshBatch, Queues, ColliderStorage, ColliderBlobs;
        public long Total => Chunks + BufferHeap + MeshGpu + MeshNative + MeshBatch + Queues + ColliderStorage + ColliderBlobs;
        public override string ToString() => $"total {Total} B; chunks {Chunks}, buffer heap {BufferHeap}, mesh GPU {MeshGpu}, mesh native {MeshNative}, mesh batch {MeshBatch}, queues {Queues}, collider storage {ColliderStorage}, collider blobs {ColliderBlobs}";
        static long Array<T>(NativeArray<T> value) where T : unmanaged => value.IsCreated ? (long)value.Length * UnsafeUtility.SizeOf<T>() : 0;
        static long List<T>(NativeList<T> value) where T : unmanaged => value.IsCreated ? (long)value.Capacity * UnsafeUtility.SizeOf<T>() + UnsafeUtility.SizeOf<UnsafeList<T>>() : 0;
        static long List<T>(UnsafeList<T> value) where T : unmanaged => value.IsCreated ? (long)value.Capacity * UnsafeUtility.SizeOf<T>() : 0;
        // Installed Collections HashMapHelper: 64-byte helper + values/keys/next arrays + 2*capacity int buckets.
        static long Map<K,V>(NativeHashMap<K,V> value) where K : unmanaged, IEquatable<K> where V : unmanaged
            => value.IsCreated ? 64L + (long)value.Capacity * (UnsafeUtility.SizeOf<K>() + UnsafeUtility.SizeOf<V>() + 12) : 0;
        static long Guard(GuardedHandles value) => value.IsCreated ? 2 * UnsafeUtility.SizeOf<Unity.Jobs.JobHandle>() : 0;
        public static unsafe VoxelMemoryStats Capture(BrickRenderFixture f)
        {
            var em = f.Em; em.CompleteAllTrackedJobs();
            var cache = f.Singleton<ColliderBuildCache>(); cache.CompleteAccess();
            var batch = f.Singleton<ColliderBatchQueue>(); batch.State.Value.Handle.Complete();
            var mesh = f.Singleton<BrickMeshBatch>(); mesh.Handle.Complete();
            var requests = f.Singleton<BrickMeshQueue>(); requests.CompleteAccess();
            var dirty = f.Singleton<ColliderDirtyQueue>(); dirty.CompleteAccess();
            var stats = new VoxelMemoryStats();
            using var query = em.CreateEntityQuery(new EntityQueryDesc { Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeSystems | EntityQueryOptions.IncludeMetaChunks });
            using var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            stats.Chunks = chunks.Length * 16384L;
            foreach (var chunk in chunks)
            {
                using var types = chunk.Archetype.GetComponentTypes(Allocator.Temp);
                foreach (var type in types)
                {
                    if (!type.IsBuffer) continue;
                    var info = TypeManager.GetTypeInfo(type.TypeIndex); var handle = em.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(type.TypeIndex));
                    var buffers = chunk.GetUntypedBufferAccessor(ref handle);
                    for (int i=0;i<chunk.Count;i++)
                    { int capacity = buffers.GetBufferCapacity(i); if (capacity > info.BufferCapacity) stats.BufferHeap += (long)capacity * info.ElementSize; }
                }
            }
            stats.MeshGpu = f.Pool.GpuBytes; stats.MeshNative = f.Pool.NativeBytes;
            stats.MeshBatch = List(mesh.Work) + Array(mesh.Snapshots) + Array(mesh.Props) + Array(mesh.Results);
            foreach (var result in mesh.Results) stats.MeshBatch += List(result.Vertices) + List(result.Indices);
            var runtime = f.Singleton<VoxelRenderRuntime>();
            stats.MeshNative += Array(runtime.PropMeshIds) + Array(runtime.PropMeshBounds);
            ref var shapes = ref runtime.Shapes.Value;
            stats.MeshNative += UnsafeUtility.SizeOf<BrickShapeTables>() + (long)shapes.Faces.Length * UnsafeUtility.SizeOf<ShapeFace>() + shapes.FirstFace.Length * 2L + shapes.FaceCount.Length + shapes.FullFaces.Length;
            var prototypes = f.Singleton<GridPrototypes>(); var retired = f.Singleton<DeferredDisposalQueue>();
            stats.Queues = List(requests.Damage)+List(requests.Neighbour)+List(requests.Spawn)+List(requests.Lod)+Array(requests.Heads)+Map(requests.Pending)+Guard(requests.Access)
                +List(dirty.Requests)+Guard(dirty.Access)+List(retired.Blobs)+List(retired.Meshes)+Map(prototypes.FunctionalByMask)+Map(prototypes.FunctionalByMaskNoRender);
            var c = cache.C; var b = batch.B;
            stats.ColliderStorage = Guard(cache.Access)+Map(c.GridIndex)+List(c.Grids)+List(c.Updates)+List(c.FreeUpdates)+Map(c.Collecting)+Map(c.CollectingBricks)
                +List(c.Masks)+List(c.FreeMasks)+List(c.Retired)+UnsafeUtility.SizeOf<ColliderPipelineStats>()+sizeof(uint)
                +UnsafeUtility.SizeOf<ColliderBatchState>()+List(b.Bricks)+List(b.Masks)+List(b.Results)+List(b.Assemblies)+List(b.AssemblyChildren)+List(b.AssemblyApply)+List(b.AssemblyResults);
            using var seen = new NativeHashSet<ulong>(4096,Allocator.Temp);
            void Blob(BlobAssetReference<Collider> blob)
            { if (blob.IsCreated && seen.Add((ulong)blob.GetUnsafePtr())) stats.ColliderBlobs += blob.Value.MemorySize; }
            foreach (var grid in c.Grids)
            {
                stats.ColliderStorage += List(grid.Children)+List(grid.Updates); Blob(grid.Installed);
                foreach (var child in grid.Children) Blob(child.Blob);
            }
            foreach (var update in c.Updates) { stats.ColliderStorage += List(update.Bricks); Blob(update.Compound); }
            foreach (var blob in c.Retired) Blob(blob);
            foreach (var blob in retired.Blobs) Blob(blob.Blob);
            foreach (var result in b.Results) Blob(result.Blob);
            foreach (var result in b.AssemblyResults) Blob(result.Compound);
            return stats;
        }
    }
}
