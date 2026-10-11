using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public static class BrickMesher
    {
        public static void MeshLod0(NativeArray<uint> snapshot, ref BrickShapeTables shapes, NativeList<PackedBrickVertex> vertices,
            NativeList<ushort> indices, out float3 min, out float3 max) => throw new System.NotImplementedException("REN-006");
        public static void MeshLod1(NativeArray<uint> snapshot, ref BrickShapeTables shapes, NativeList<PackedBrickVertex> vertices,
            NativeList<ushort> indices, out float3 min, out float3 max) => throw new System.NotImplementedException("REN-007");
    }
}
