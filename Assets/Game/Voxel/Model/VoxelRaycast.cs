using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.5. Allocation-free grid-space DDA; distance is in cells, independent of direction length.</summary>
    public static class VoxelRaycast
    {
        public static bool Cast<T>(ref T query, float3 originCells, float3 dir, float maxCells, out VoxelRayHit hit) where T : struct, ICellSolidQuery
        {
            hit = default;
            if (!math.all(math.isfinite(originCells)) || !math.all(math.isfinite(dir)) || !math.isfinite(maxCells) || maxCells < 0) return false;
            // Double boundary distances avoid accumulated float drift on long rays. Inputs and the public result remain grid-local floats.
            double length = math.length((double3)dir);
            if (length == 0) return false;
            double3 direction = (double3)dir / length;
            int3 cell = (int3)math.floor(originCells);
            if (query.IsSolid(cell)) { hit = new VoxelRayHit { Cell = cell, EnteredFace = 255 }; return true; }
            int3 step = (int3)math.sign(direction);
            double3 delta = math.abs(1.0 / direction);
            double3 next = new double3(double.PositiveInfinity);
            for (int axis = 0; axis < 3; axis++)
                if (step[axis] != 0) next[axis] = ((double)cell[axis] + (step[axis] > 0 ? 1 : 0) - originCells[axis]) / direction[axis];
            while (true)
            {
                int3 brick = BrickMath.BrickOf(cell);
                bool exists = query.BrickExists(brick);
                double distance;
                byte face;
                // The empty-brick path performs no cell or map queries until it leaves that brick.
                // Keeping individual boundary events preserves X/Y/Z order even at edges and corners.
                do
                {
                    int axis = next.x <= next.y && next.x <= next.z ? 0 : next.y <= next.z ? 1 : 2;
                    distance = next[axis];
                    if (distance > maxCells) return false;
                    cell[axis] += step[axis]; next[axis] += delta[axis];
                    face = (byte)(axis * 2 + (step[axis] > 0 ? 1 : 0));
                }
                while (!exists && math.all(BrickMath.BrickOf(cell) == brick));
                if (query.IsSolid(cell))
                {
                    hit = new VoxelRayHit { Cell = cell, EnteredFace = face, DistanceCells = (float)distance }; return true;
                }
            }
        }
    }
}
