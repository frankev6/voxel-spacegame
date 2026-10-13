using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.Galaxy.Bodies
{
    /// <summary>SPEC-31 §5.8: simplex-noise occupancy at physical cell centres, with one disjoint dense span per brick.</summary>
    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Strict)]
    internal struct AsteroidFillJob : IJobParallelFor
    {
        public float Radius, CellSize, Amplitude, Frequency;
        public float3 NoiseOffset;
        public int3 Min, Size, BrickMin, BrickDims;
        // No two Execute indices write the same cell; staging is separate from the main-thread-owned structure.
        [NativeDisableParallelForRestriction] public NativeArray<Cell> Cells;
        public void Execute(int index)
        {
            int3 brick = StructureBuilderAssembly.Coord(index, BrickMin, BrickDims);
            for (int local = 0; local < BrickMath.CellsPerBrick; local++)
            {
                int3 cell = BrickMath.CellOf(brick, local); float3 point = BrickMath.CellCenter(cell, CellSize);
                bool solid = math.all(cell >= Min) && math.all(cell < Min + Size) &&
                    math.length(point) <= Radius * (1 + Amplitude * noise.snoise(point * Frequency + NoiseOffset));
                Cells[index * BrickMath.CellsPerBrick + local] = solid ? Cell.Palette(1) : default;
            }
        }
    }
}
