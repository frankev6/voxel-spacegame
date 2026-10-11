using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct BrickMeshWork
    {
        public Entity Brick, Grid; public byte Lod;
        public int VertexCount, IndexCount;
        public float3 BoundsMin, BoundsMax;
        public int PropStart, PropCount;
    }
}
