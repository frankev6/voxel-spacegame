using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Voxel.Rendering
{
    /// <summary>Implements SPEC-11 §4 (rendering contract).</summary>
    public static class BrickSnapshotUtility
    {
        public static void FromStructure(Game.Voxel.Model.VoxelStructureData data, int3 brick, ref Game.Data.Blocks.BlockDefsBlob blocks,
            ref BrickShapeTables shapes, NativeArray<uint> output) => throw new System.NotImplementedException("REN-005");
    }
}
