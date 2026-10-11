using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace Game.Voxel.Data
{
    /// <summary>SPEC-05 §5.7: bounded run loops, shared by Burst direct calls and managed fallback.</summary>
    [BurstCompile]
    internal static unsafe class VoxsRuns
    {
        internal static int Index(int n, int order)
        {
            int a = n & 15, b = (n >> 4) & 15, c = n >> 8;
            return order == 1 ? b + 16 * (a + 16 * c) : order == 2 ? b + 16 * (c + 16 * a) : n;
        }
        static int Value(Cell c, int field) => field == 0 ? c.Ref : field == 1 ? c.Hp : c.Flags;
        static int VarSize(uint v) { int n = 1; while (v >= 128) { n++; v >>= 7; } return n; }
        static void Var(UnsafeList<byte>* output, uint v)
        {
            while (v >= 128) { output->Add((byte)(v | 128)); v >>= 7; }
            output->Add((byte)v);
        }
        static void Run(UnsafeList<byte>* output, int value, int count, int field)
        {
            output->Add((byte)value);
            if (field == 0) output->Add((byte)(value >> 8));
            Var(output, (uint)(count - 1));
        }
        [BurstCompile]
        internal static int MeasureRuns(Cell* cells, int order)
        {
            int size = 0, value = -1, count = 0;
            for (int n = 0; n < 4096; n++)
            {
                int next = cells[Index(n, order)].Ref;
                if (next == value) count++;
                else { if (count != 0) size += 2 + VarSize((uint)(count - 1)); value = next; count = 1; }
            }
            return size + 2 + VarSize((uint)(count - 1));
        }
        [BurstCompile]
        internal static void EncodeRuns(Cell* cells, int order, int field, UnsafeList<byte>* output)
        {
            int value = -1, count = 0;
            for (int n = 0; n < 4096; n++)
            {
                Cell c = cells[Index(n, order)];
                if (field != 0 && c.Ref == 0) continue;
                int next = Value(c, field);
                if (next == value) count++;
                else { if (count != 0) Run(output, value, count, field); value = next; count = 1; }
            }
            if (count != 0) Run(output, value, count, field);
        }
        // Returns consumed bytes, or -1. Offset always identifies the failing read.
        [BurstCompile]
        internal static int DecodeRuns(byte* bytes, int length, Cell* cells, int order, int field, int total, out int offset)
        {
            offset = 0;
            int written = 0, traversal = 0;
            while (written < total)
            {
                int width = field == 0 ? 2 : 1;
                if (length - offset < width) return -1;
                int value = bytes[offset++];
                if (width == 2) value |= bytes[offset++] << 8;
                uint run = 0;
                for (int b = 0; b < 5; b++)
                {
                    if (offset >= length) return -1;
                    byte v = bytes[offset++];
                    if (b == 4 && v > 15) return -1;
                    run |= (uint)(v & 127) << (b * 7);
                    if ((v & 128) == 0) break;
                }
                if (run >= (uint)(total - written)) return -1;
                int count = (int)run + 1;
                for (int j = 0; j < count; j++)
                {
                    if (field != 0)
                        while (traversal < 4096 && cells[Index(traversal, order)].Ref == 0) traversal++;
                    if (traversal >= 4096) return -1;
                    Cell* c = cells + Index(traversal++, order);
                    if (field == 0) c->Ref = (ushort)value;
                    else if (field == 1) c->Hp = (byte)value;
                    else c->Flags = (byte)value;
                }
                written += count;
            }
            return offset;
        }
    }
}
