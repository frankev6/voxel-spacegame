using Unity.Entities;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.5. A mesh waiting for deferred disposal (§5.10).</summary>
    public struct RetiredMesh { public UnityObjectRef<Mesh> Mesh; public BatchMeshID Id; public uint DueFrame; }
}
