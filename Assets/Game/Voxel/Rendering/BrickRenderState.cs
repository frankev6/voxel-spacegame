using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct BrickRenderState : ICleanupComponentData
    {
        public BrickPoolHandle Handle;
        public PoolRange Vertices, Indices;
        public int VertexCount, IndexCount;
        public byte ShownLod, HasMesh;
        public ushort Reserved;
    }
}
