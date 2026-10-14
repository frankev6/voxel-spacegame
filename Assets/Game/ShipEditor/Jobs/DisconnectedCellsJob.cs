using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Game.ShipEditor.Jobs
{
    [BurstCompile]
    internal struct DisconnectedCellsJob : IJob
    {
        [ReadOnly] public NativeArray<int3> Bricks;
        [ReadOnly] public NativeArray<int> Labels, Parents;
        public NativeList<int3> Cells;
        public int Root, Limit;
        public void Execute()
        {
            Cells.Clear();
            for (int i = 0; i < Labels.Length && Cells.Length < Limit; i++)
                if (Labels[i] >= 0 && ConnectivityCells.Root(Labels[i], Parents) != Root) Cells.Add(ConnectivityCells.Cell(i, Bricks));
        }
    }
}
