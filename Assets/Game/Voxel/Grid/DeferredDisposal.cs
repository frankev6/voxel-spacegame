using Game.Voxel.Model;
using Unity.Entities;
using Collider = Unity.Physics.Collider;
using Mesh = UnityEngine.Mesh;
using UnityEngine.Rendering;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §5.10. Main-thread helpers that retire collider blobs and meshes for DeferredDisposalSystem.</summary>
    public static class DeferredDisposal
    {
        public static void RetireBlob(ref DeferredDisposalQueue q, BlobAssetReference<Collider> b)
        {
            if (b.IsCreated) q.Blobs.Add(new RetiredBlob { Blob = b, DueFrame = q.Frame + 3, DueTick = q.Tick + 2 });
        }
        public static void RetireMesh(ref DeferredDisposalQueue q, UnityObjectRef<Mesh> mesh, BatchMeshID id)
        {
            if (mesh.Value != null || id != BatchMeshID.Null)
                q.Meshes.Add(new RetiredMesh { Mesh = mesh, Id = id, DueFrame = q.Frame + 3 });
        }
    }
}
