using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.ShipEditor.Jobs
{
    [BurstCompile]
    internal struct BoundedSplitJob : IJob
    {
        [ReadOnly] public NativeHashMap<int3,int> Slots;
        [ReadOnly] public NativeArray<int3> Bricks;
        [ReadOnly] public NativeArray<int> Labels;
        public NativeList<int> Unresolved, Visited;
        public NativeHashSet<int> Seen;
        public NativeReference<bool> BudgetHit;
        public int Budget;
        public void Execute()
        {
            Visited.Clear(); Seen.Clear(); BudgetHit.Value = false;
            int start = Unresolved[0]; Seen.Add(start); Visited.Add(start);
            for (int cursor = 0; cursor < Visited.Length; cursor++)
            {
                int at = Visited[cursor];
                for (int i = Unresolved.Length - 1; i >= 0; i--) if (Unresolved[i] == at) Unresolved.RemoveAtSwapBack(i);
                if (Unresolved.Length == 0) return;
                int3 cell = ConnectivityCells.Cell(at, Bricks);
                for (int d = 0; d < 6; d++)
                {
                    int n = ConnectivityCells.Index(cell + ConnectivityCells.Direction(d), Slots);
                    if (n < 0 || Labels[n] < 0 || Seen.Contains(n)) continue;
                    if (Visited.Length == Budget) { BudgetHit.Value = true; return; }
                    Seen.Add(n); Visited.Add(n);
                }
            }
        }
    }
}
