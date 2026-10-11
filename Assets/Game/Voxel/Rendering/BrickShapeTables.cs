using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public struct BrickShapeTables
    {
        public BlobArray<ShapeFace> Faces;
        public BlobArray<ushort> FirstFace;
        public BlobArray<byte> FaceCount;
        public BlobArray<byte> FullFaces;
    }
}
