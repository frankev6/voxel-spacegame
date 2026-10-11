using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Game.Voxel.Rendering;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Game.Voxel.Grid
{
    /// <summary>World-owned spawn/render/physics storage, created before the boot entity snapshot (SPEC-10 §5.7).</summary>
    internal static class VoxelSingletons
    {
        internal static Entity CreateAll(EntityManager em)
        {
            Entity e = em.CreateEntity();
            em.AddComponentData(e, new GridPrototypes
            {
                NextSerial = 1,
                FunctionalByMask = new NativeHashMap<ulong, Entity>(32, Allocator.Persistent),
                FunctionalByMaskNoRender = new NativeHashMap<ulong, Entity>(32, Allocator.Persistent)
            });
            em.AddComponentData(e, new DeferredDisposalQueue
            {
                Blobs = new NativeList<RetiredBlob>(1024, Allocator.Persistent),
                Meshes = new NativeList<RetiredMesh>(1024, Allocator.Persistent)
            });
            em.AddBuffer<FunctionalComponentRegistration>(e);
            em.AddBuffer<RootComponentRegistration>(e);
            em.AddComponentData(e, BrickMeshQueue.Create());
            em.AddComponentData(e, BrickMeshBatch.Create());
            em.AddComponentData(e, VoxelRenderRuntime.Create());
            em.AddComponentData(e, new VoxelViewer());
            em.AddComponentData(e, ColliderDirtyQueue.Create());
            em.AddComponentData(e, ColliderBuildCache.Create());
            em.AddComponentData(e, ColliderBatchQueue.Create());
            return e;
        }

        internal static void DisposeAll(EntityManager em, Entity e)
        {
            if (!em.Exists(e)) return;
            em.CompleteAllTrackedJobs();
            em.GetComponentData<ColliderBatchQueue>(e).Dispose();
            em.GetComponentData<ColliderBuildCache>(e).Dispose();
            em.GetComponentData<ColliderDirtyQueue>(e).Dispose();
            em.GetComponentData<BrickMeshBatch>(e).Dispose();
            em.GetComponentData<BrickMeshQueue>(e).Dispose();
            em.GetComponentData<VoxelRenderRuntime>(e).Dispose();
            var retired = em.GetComponentData<DeferredDisposalQueue>(e);
            foreach (var entry in retired.Blobs) { var blob = entry.Blob; if (blob.IsCreated) blob.Dispose(); }
            // World shutdown releases Entities Graphics registrations; system lookup is forbidden during destruction.
            foreach (var entry in retired.Meshes)
            {
                var mesh = entry.Mesh.Value;
                if (mesh != null) { if (Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh); }
            }
            retired.Blobs.Dispose(); retired.Meshes.Dispose();
            var prototypes = em.GetComponentData<GridPrototypes>(e);
            prototypes.FunctionalByMask.Dispose(); prototypes.FunctionalByMaskNoRender.Dispose();
            em.DestroyEntity(e);
        }
    }
}
