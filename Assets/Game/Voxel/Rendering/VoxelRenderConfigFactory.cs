using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public static class VoxelRenderConfigFactory
    {
        public static VoxelRenderConfig Create(Game.Data.Definitions.VoxelRenderConfigDefinition def, System.Collections.Generic.IReadOnlyList<Mesh> propMeshes)
            => throw new System.NotImplementedException("REN-009");
        public static void Destroy(ref VoxelRenderConfig c) => throw new System.NotImplementedException("REN-009");
    }
}
