using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Services
{
    /// <summary>SPEC-51 face-neighbour invalidation; negative cells use arithmetic floor division.</summary>
    internal sealed class DirtyBrickSet
    {
        readonly HashSet<int3> bricks = new();
        internal int Count => bricks.Count;
        internal void Add(int3 brick) => bricks.Add(brick);
        internal void AddCell(int3 cell)
        {
            int3 brick = cell >> 4, local = cell & 15;
            bricks.Add(brick);
            for (int axis = 0; axis < 3; axis++)
            {
                if (local[axis] != 0 && local[axis] != 15) continue;
                var neighbour = brick; neighbour[axis] += local[axis] == 0 ? -1 : 1; bricks.Add(neighbour);
            }
        }
        internal void Drain(NativeList<int3> output)
        {
            output.Clear(); foreach (var brick in bricks) output.Add(brick); bricks.Clear();
        }
        internal void Clear() => bricks.Clear();
    }
}
