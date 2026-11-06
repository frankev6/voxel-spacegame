using Game.Voxel.Model;
using Unity.Burst;

namespace Game.Voxel.Data
{
    // PERS-021: synchronous integer loops over already bounded decoder-owned buffers.
    // No allocation, retained pointers, relaxed validation, or parallel writes.
    [BurstCompile]
    internal static unsafe class VoxsCellLoops
    {
        [BurstCompile]
        internal static void FillReferences(Cell* brick, ushort value)
        {
            for (int i = 0; i < 4096; i++) brick[i].Ref = value;
        }
        [BurstCompile]
        internal static int ValidateReferencesAndInitializeHp(Cell* brick, int instances, int palettes)
        {
            int solid = 0;
            for (int i = 0; i < 4096; i++)
            {
                var cell = brick[i];
                if (cell.Ref == 0) continue;
                if (cell.IsInstance ? cell.InstanceIndex >= instances : cell.Ref >= palettes) return -1;
                brick[i].Hp = 255; solid++;
            }
            return solid;
        }
        [BurstCompile]
        internal static int ValidateHp(Cell* brick)
        {
            for (int i = 0; i < 4096; i++)
                if (brick[i].Ref != 0 && brick[i].Hp == 0) return 0;
            return 1;
        }
        [BurstCompile]
        internal static void CountInstances(Cell* cells, int length, int* counts)
        {
            for (int i = 0; i < length; i++) if (cells[i].IsInstance) counts[cells[i].InstanceIndex]++;
        }
        [BurstCompile]
        internal static void RemapInstances(Cell* cells, int length, ushort* remap)
        {
            for (int i = 0; i < length; i++) if (cells[i].IsInstance) cells[i].Ref = remap[cells[i].InstanceIndex];
        }
    }
}
