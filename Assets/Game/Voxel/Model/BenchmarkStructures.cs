using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.11. Deterministic benchmark structures built from one single-cell Structural Cube block, colour 0.</summary>
    public static class BenchmarkStructures
    {
        enum Shape : byte { Box, Shell, Sphere, Lattice }
        public static VoxelStructure HollowStation1M(ushort cubeBlock) => Build(cubeBlock, new int3(256, 128, 256), Shape.Shell, 4);
        public static VoxelStructure ThinShellStation1M(ushort cubeBlock) => Build(cubeBlock, new int3(512, 256, 512), Shape.Shell, 1);
        public static VoxelStructure SolidAsteroid1M(ushort cubeBlock) => Build(cubeBlock, new int3(128), Shape.Sphere);
        public static VoxelStructure Ship20k(ushort cubeBlock) => Build(cubeBlock, new int3(80, 10, 25), Shape.Box);
        public static VoxelStructure Lattice2500(ushort cubeBlock) => Build(cubeBlock, new int3(400, 64, 400), Shape.Lattice);

        static VoxelStructure Build(ushort block, int3 dimensions, Shape shape, int wall = 0)
        {
            if (block == 0) throw new ArgumentOutOfRangeException(nameof(block));
            var structure = new VoxelStructure();
            try
            {
                ushort palette = structure.FindOrAddPalette(new PaletteEntry { Block = block });
                Cell solid = Cell.Palette(palette);
                var cells = new NativeArray<Cell>(BrickMath.CellsPerBrick, Allocator.Temp);
                try
                {
                    int3 bricks = (dimensions + 15) >> 4;
                    if (shape == Shape.Lattice) cells[0] = solid;
                    for (int bz = 0; bz < bricks.z; bz++) for (int by = 0; by < bricks.y; by++) for (int bx = 0; bx < bricks.x; bx++)
                    {
                        int3 coord = new int3(bx, by, bz), origin = coord << 4;
                        if (shape == Shape.Lattice) { structure.SetBrickDense(coord, cells); continue; }
                        if (shape == Shape.Shell && math.all(origin >= wall) && math.all(origin + 16 <= dimensions - wall)) continue;
                        if (shape == Shape.Sphere)
                        {
                            // Twice the distance from cell centres to (64,64,64); all classification is exact integer arithmetic.
                            int3 min = 2 * origin - 127, max = min + 30;
                            int3 nearest = math.max(math.max(min, -max), 0), farthest = math.max(math.abs(min), math.abs(max));
                            if (Squared(nearest) > 124 * 124) continue;
                            if (Squared(farthest) <= 124 * 124) { structure.SetBrickUniform(coord, solid); continue; }
                        }
                        if (shape == Shape.Box && math.all(origin + 16 <= dimensions)) { structure.SetBrickUniform(coord, solid); continue; }
                        int count = 0;
                        for (int i = 0; i < cells.Length; i++)
                        {
                            int3 cell = origin + BrickMath.LocalFromIndex(i);
                            bool filled = math.all(cell < dimensions);
                            if (shape == Shape.Shell) filled &= math.any(cell < wall) || math.any(cell >= dimensions - wall);
                            else if (shape == Shape.Sphere) filled &= Squared(2 * cell - 127) <= 124 * 124;
                            cells[i] = filled ? solid : default;
                            if (filled) count++;
                        }
                        if (count == BrickMath.CellsPerBrick) structure.SetBrickUniform(coord, solid);
                        else if (count != 0) structure.SetBrickDense(coord, cells);
                    }
                }
                finally { cells.Dispose(); }
                structure.OptimizeStorage(); return structure;
            }
            catch { structure.Dispose(); throw; }
        }
        static int Squared(int3 value) => value.x * value.x + value.y * value.y + value.z * value.z;
    }
}
