using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Amanatides–Woo 3D DDA over integer cells. Implements SPEC-13 §5.3.3.</summary>
    public struct GridDda
    {
        int3 current, step, min, max;
        float3 crossing, delta;
        bool ready;

        public bool Init(float3 startCells, float3 dir, int3 aabbMin, int3 aabbMaxExclusive)
        {
            this = default;
            if (!math.all(math.isfinite(startCells)) || !math.all(math.isfinite(dir)) ||
                math.all(dir == 0) || math.any(aabbMaxExclusive <= aabbMin)) return false;
            min = aabbMin; max = aabbMaxExclusive;
            float enter = 0f, exit = float.PositiveInfinity;
            for (int axis = 0; axis < 3; axis++)
            {
                if (dir[axis] == 0f)
                {
                    if (startCells[axis] < min[axis] || startCells[axis] >= max[axis]) return false;
                    continue;
                }
                float a = (min[axis] - startCells[axis]) / dir[axis];
                float b = (max[axis] - startCells[axis]) / dir[axis];
                enter = math.max(enter, math.min(a, b)); exit = math.min(exit, math.max(a, b));
            }
            if (exit <= enter) return false;
            float3 p = startCells + dir * enter;
            current = (int3)math.floor(p);
            for (int axis = 0; axis < 3; axis++)
            {
                step[axis] = dir[axis] > 0f ? 1 : dir[axis] < 0f ? -1 : 0;
                // A negative ray on a boundary enters the cell below it, not a zero-length cell above.
                if (dir[axis] < 0f && p[axis] == current[axis]) current[axis]--;
                current[axis] = math.clamp(current[axis], min[axis], max[axis] - 1);
                delta[axis] = dir[axis] == 0f ? float.PositiveInfinity : math.abs(1f / dir[axis]);
                crossing[axis] = dir[axis] == 0f ? float.PositiveInfinity :
                    (current[axis] + (step[axis] > 0 ? 1 : 0) - startCells[axis]) / dir[axis];
            }
            ready = true; return true;
        }

        public bool Next(out int3 cell)
        {
            cell = current;
            if (!ready) return false;
            float next = math.cmin(crossing);
            // Cross all tied axes together: cells touched only at an edge/corner have no ray interval.
            for (int axis = 0; axis < 3; axis++)
                if (crossing[axis] == next) { current[axis] += step[axis]; crossing[axis] += delta[axis]; }
            ready = math.all(current >= min) && math.all(current < max);
            return true;
        }
    }
}
