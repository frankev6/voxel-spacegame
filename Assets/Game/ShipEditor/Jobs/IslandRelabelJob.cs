using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Game.Voxel.Model;

namespace Game.ShipEditor.Jobs
{
    internal static class ConnectivityCells
    {
        internal static int3 Direction(int axis) => axis switch { 0 => new int3(1,0,0), 1 => new int3(-1,0,0), 2 => new int3(0,1,0), 3 => new int3(0,-1,0), 4 => new int3(0,0,1), _ => new int3(0,0,-1) };
        internal static int Index(int3 cell, NativeHashMap<int3,int> slots) => slots.TryGetValue(BrickMath.BrickOf(cell), out int slot) ? slot * 4096 + BrickMath.LocalIndex(BrickMath.LocalOf(cell)) : -1;
        internal static int3 Cell(int index, NativeArray<int3> bricks) => BrickMath.CellOf(bricks[index / 4096], index % 4096);
        internal static int Root(int label, NativeArray<int> parents) { while (label >= 0 && parents[label] != label) label = parents[label]; return label; }
    }
    [BurstCompile]
    internal struct IslandRelabelJob : IJob
    {
        [ReadOnly] public NativeHashMap<int3,int> Slots;
        [ReadOnly] public NativeArray<int3> Bricks;
        [ReadOnly] public NativeArray<int> Input;
        public NativeArray<int> Output;
        public NativeList<int> Parents, Sizes, Stack;
        public void Execute()
        {
            Parents.Clear(); Sizes.Clear(); Stack.Clear();
            for (int i = 0; i < Output.Length; i++) Output[i] = -1;
            for (int i = 0; i < Input.Length; i++)
            {
                if (Input[i] < 0 || Output[i] >= 0) continue;
                int root = Parents.Length, count = 0; Parents.Add(root); Sizes.Add(0);
                Output[i] = root; Stack.Add(i);
                while (Stack.Length > 0)
                {
                    int at = Stack[Stack.Length - 1]; Stack.RemoveAt(Stack.Length - 1); count++;
                    int3 cell = ConnectivityCells.Cell(at, Bricks);
                    for (int d = 0; d < 6; d++)
                    {
                        int n = ConnectivityCells.Index(cell + ConnectivityCells.Direction(d), Slots);
                        if (n < 0 || Input[n] < 0 || Output[n] >= 0) continue;
                        Output[n] = root; Stack.Add(n);
                    }
                }
                Sizes[root] = count;
            }
        }
    }
}
