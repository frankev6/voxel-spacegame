using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct ShapeFace
    {
        public byte Plane, Normal, VertexCount, IsFull;
        public int3 V0, V1, V2, V3; // Half-cell units (0..2) within one cell, ADR-0027.
    }
}
