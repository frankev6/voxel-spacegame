using Unity.Mathematics;
using Unity.Transforms;
using Game.Data.Blocks;

namespace Game.Simulation.Common
{
    /// <summary>Grid-space geometry of block instances (SPEC-04 decisions 8–9). Used by SPEC-20/21/22. Implements SPEC-20 §4.5.</summary>
    public static class GridSpaceMath
    {
        public static float3 CellCenter(int3 cell, float cellSize) => ((float3)cell + 0.5f) * cellSize;
        /// Centre of the rotated footprint bounding box: (origin + 0.5 + R(o)·((BoundsMin + BoundsMax) / 2)) × cellSize.
        public static float3 InstanceCenter(ref BlockDef def, ref OrientationTablesBlob ot, byte orient, int3 origin, float cellSize)
            => BlockPointToGrid(((float3)def.BoundsMin + (float3)def.BoundsMax) * 0.5f, ref ot, orient, origin, cellSize);
        /// Block-local point in cells relative to the root cell centre → grid-local metres: (origin + 0.5 + R(o)·p) × cellSize.
        public static float3 BlockPointToGrid(float3 blockLocalCells, ref OrientationTablesBlob ot, byte orient, int3 origin, float cellSize)
            => ((float3)origin + 0.5f + ot.Rotate(blockLocalCells, orient)) * cellSize;
        /// Block-local direction → grid-local direction (R(o)·d; length preserved).
        public static float3 BlockDirToGrid(float3 dir, ref OrientationTablesBlob ot, byte orient) => ot.Rotate(dir, orient);
        public static float3 GridToWorldPoint(in LocalTransform root, float3 gridLocal) => root.TransformPoint(gridLocal);
        public static float3 GridToWorldDir(in LocalTransform root, float3 gridDir) => math.rotate(root.Rotation, gridDir);
        public static float3 WorldToGridPoint(in LocalTransform root, float3 world) => root.InverseTransformPoint(world);
        public static float3 WorldToGridDir(in LocalTransform root, float3 worldDir) => math.rotate(math.inverse(root.Rotation), worldDir);
    }
}
