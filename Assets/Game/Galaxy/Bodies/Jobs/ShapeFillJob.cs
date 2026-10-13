using Game.Data.Definitions;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Bodies
{
    internal struct NearestShapeCell
    {
        public int3 Cell;
        public long DistanceSquared;
        public byte Found;
        public bool Before(NearestShapeCell other) => Found != 0 && (other.Found == 0 || DistanceSquared < other.DistanceSquared ||
            DistanceSquared == other.DistanceSquared && (Cell.z < other.Cell.z || Cell.z == other.Cell.z &&
            (Cell.y < other.Cell.y || Cell.y == other.Cell.y && Cell.x < other.Cell.x)));
    }
    /// <summary>SPEC-31 §5.8: one independently owned dense output span and nearest-core candidate per brick.</summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal struct ShapeFillJob : IJobParallelFor
    {
        public ShapeKind Kind;
        public int3 Min, Size, BrickMin, BrickDims;
        public int Thickness;
        public float InnerRadius;
        // Execute(k) owns only [k*4096, (k+1)*4096); every element is initialized, including outside-shape air.
        [NativeDisableParallelForRestriction] public NativeArray<Cell> Cells;
        public NativeArray<NearestShapeCell> Closest;
        public void Execute(int index)
        {
            int3 brick = StructureBuilderAssembly.Coord(index, BrickMin, BrickDims); NearestShapeCell nearest = default;
            for (int local = 0; local < BrickMath.CellsPerBrick; local++)
            {
                int3 cell = BrickMath.CellOf(brick, local); bool solid = Contains(cell);
                Cells[index * BrickMath.CellsPerBrick + local] = solid ? Cell.Palette(1) : default;
                if (!solid) continue;
                long x = 2L * cell.x + 1 - (2L * Min.x + Size.x), y = 2L * cell.y + 1 - (2L * Min.y + Size.y), z = 2L * cell.z + 1 - (2L * Min.z + Size.z);
                var candidate = new NearestShapeCell { Cell = cell, Found = 1, DistanceSquared = x * x + y * y + z * z };
                if (candidate.Before(nearest)) nearest = candidate;
            }
            Closest[index] = nearest;
        }
        bool Contains(int3 cell)
        {
            int3 c = cell - Min;
            if (math.any(c < 0) || math.any(c >= Size)) return false;
            if (Kind == ShapeKind.Box) return Thickness == 0 || math.any(c < Thickness) || math.any(c >= Size - Thickness);
            float3 q = (float3)c + .5f - (float3)Size * .5f;
            float radius = Size.x * .5f, inner = math.max(0, radius - Thickness);
            float squared = Kind == ShapeKind.Sphere ? math.lengthsq(q) : math.lengthsq(q.xz);
            if (squared > radius * radius || Kind == ShapeKind.Ring && squared < InnerRadius * InnerRadius) return false;
            if (Thickness == 0 || inner <= 0) return true;
            if (Kind == ShapeKind.Sphere) return squared > inner * inner;
            bool cavity = math.abs(q.y) < Size.y * .5f - Thickness && squared <= inner * inner;
            if (Kind == ShapeKind.Ring) cavity &= inner > InnerRadius + Thickness && squared >= (InnerRadius + Thickness) * (InnerRadius + Thickness);
            return !cavity;
        }
    }
}
