using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public static class VoxelNormals
    {
        public const int Count = 42; // ADR-0018: 18 axis/diagonal + 24 long-slope normals.
        public static float3 Normal(int i) => throw new System.NotImplementedException("REN-004");
        public static float3 Tangent(int i) => throw new System.NotImplementedException("REN-004");
        public static int IndexOf(int3 scaledNormal) => throw new System.NotImplementedException("REN-004");
    }
}
