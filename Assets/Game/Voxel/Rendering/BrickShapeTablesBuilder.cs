using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public static class BrickShapeTablesBuilder
    {
        public static BlobAssetReference<BrickShapeTables> Build(ref Game.Data.Blocks.OrientationTablesBlob orientations)
            => throw new System.NotImplementedException("REN-004");
    }
}
