using UnityEngine;

namespace Game.Voxel.Rendering
{
    /// <summary>World-owned graphics resources for SPEC-11 §5.10; never one material or mesh per brick.</summary>
    public sealed class VoxelRenderManagedState : ScriptableObject
    {
        internal BrickPoolRenderer Pool;
        internal Material BrickMaterial, PropMaterial;
        internal Mesh Placeholder;
        internal MaterialPropertyBlock[] PropBlocks;
        internal GraphicsBuffer[] PropInstanceBuffers;
        internal GraphicsBuffer GridMatrices;
        internal System.Collections.Generic.List<int> PropCounts = new System.Collections.Generic.List<int>();
        internal void Dispose()
        {
            Pool?.Dispose(); Pool = null;
            if (PropInstanceBuffers != null) foreach (var buffer in PropInstanceBuffers) buffer?.Dispose();
            PropInstanceBuffers = null; GridMatrices?.Dispose(); GridMatrices = null;
            Release(BrickMaterial); Release(PropMaterial); Release(Placeholder);
            BrickMaterial = null; PropMaterial = null; Placeholder = null;
        }
        static void Release(Object value)
        { if (value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
        void OnDestroy() => Dispose();
    }
}
