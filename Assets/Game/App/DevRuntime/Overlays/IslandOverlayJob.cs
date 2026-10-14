#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
namespace Game.App.DevRuntime.Overlays
{
    /// <summary>SPEC-70 DEV-013: bounded six-neighbour flood over a read-only snapshot of real brick masks.</summary>
    [BurstCompile]
    internal struct IslandOverlayJob : IJob
    {
        public const int MaxCells = 2000000;
        public const int MaxBounds = DevLineBatch.MaxVertices / 24;
        public int3 Min, Max;
        [ReadOnly] public NativeParallelHashMap<int3, int> BrickOffsets;
        [ReadOnly] public NativeArray<ulong> Masks;
        public NativeList<IslandOverlayBounds> Islands;
        public NativeReference<IslandOverlayState> Result;
        // Long differences and early checks keep oversized or extreme bounds out of allocations/index arithmetic.
        internal static int CellCount(int3 min, int3 max)
        {
            long x = (long)max.x - min.x + 1, y = (long)max.y - min.y + 1, z = (long)max.z - min.z + 1;
            if (x <= 0 || y <= 0 || z <= 0) return 0;
            if (x > MaxCells || y > MaxCells || z > MaxCells || x * y > MaxCells || x * y * z > MaxCells) return -1;
            return (int)(x * y * z);
        }
        bool Solid(int3 cell)
        {
            if (!BrickOffsets.TryGetValue(BrickMath.BrickOf(cell), out int start)) return false;
            int local = BrickMath.LocalIndex(BrickMath.LocalOf(cell));
            return (Masks[start + (local >> 6)] & (1UL << (local & 63))) != 0;
        }
        int3 Cell(int index, int3 dims) => Min + new int3(index % dims.x, index / dims.x % dims.y, index / (dims.x * dims.y));
        public void Execute()
        {
            Islands.Clear(); var result = new IslandOverlayState();
            int count = CellCount(Min, Max);
            if (count <= 0) { result.Skipped = count < 0 ? 1 : 0; Result.Value = result; return; }
            int3 dims = Max - Min + 1;
            using var visited = new NativeBitArray(count, Allocator.Temp);
            using var frontier = new NativeList<int>(Allocator.Temp);
            for (int seed = 0; seed < count; seed++)
            {
                if (visited.IsSet(seed)) continue;
                visited.Set(seed, true);
                int3 first = Cell(seed, dims);
                if (!Solid(first)) continue;
                var bounds = new IslandOverlayBounds { Min = first, Max = first, Index = result.Count };
                frontier.Clear(); frontier.Add(seed);
                for (int head = 0; head < frontier.Length; head++)
                {
                    int3 cell = Cell(frontier[head], dims);
                    bounds.Min = math.min(bounds.Min, cell); bounds.Max = math.max(bounds.Max, cell); bounds.SolidCount++;
                    for (int axis = 0; axis < 3; axis++)
                        for (int sign = -1; sign <= 1; sign += 2)
                        {
                            // Test the boundary before addition, including extreme signed cell coordinates.
                            if (sign < 0 ? cell[axis] <= Min[axis] : cell[axis] >= Max[axis]) continue;
                            int3 next = cell; next[axis] += sign;
                            int3 local = next - Min;
                            int index = local.x + dims.x * (local.y + dims.y * local.z);
                            if (visited.IsSet(index)) continue;
                            visited.Set(index, true);
                            if (Solid(next)) frontier.Add(index);
                        }
                }
                if (Islands.Length < MaxBounds) Islands.Add(bounds);
                if (bounds.SolidCount > result.Largest.SolidCount) result.Largest = bounds;
                result.Count++;
            }
            Result.Value = result;
        }
    }
}
#endif
