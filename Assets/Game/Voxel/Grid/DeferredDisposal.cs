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
        public static void RetireBlob(ref DeferredDisposalQueue q, BlobAssetReference<Collider> b) { throw new System.NotImplementedException("VOX-016"); }   // DueFrame = Frame + 3, DueTick = Tick + 2
        public static void RetireMesh(ref DeferredDisposalQueue q, UnityObjectRef<Mesh> mesh, BatchMeshID id) { throw new System.NotImplementedException("VOX-016"); }   // DueFrame = Frame + 3
    }
}
