using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    // Public type required by VoxelRenderRuntime.Managed; mutable implementation fields stay internal.
    public sealed class VoxelRenderManagedState : ScriptableObject
    {
        internal System.Collections.Generic.List<Mesh> ApplyList = new System.Collections.Generic.List<Mesh>();
        internal Mesh Scratch = null;
        internal Mesh.MeshDataArray PendingData = default;
        internal bool HasPendingData = false;
        internal MaterialPropertyBlock[] PropBlocks = null;
        internal GraphicsBuffer[] PropInstanceBuffers = null;
        internal GraphicsBuffer GridMatrices = null;
        internal System.Collections.Generic.List<int> PropCounts = new System.Collections.Generic.List<int>();
    }
}
