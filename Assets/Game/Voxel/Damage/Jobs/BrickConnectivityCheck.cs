using Game.Voxel.Grid;
using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.4: bounded 16-bit scanline flood, followed by unions across instance references.
    /// Called inside the Burst integrity job; all scratch memory is local to this brick check.</summary>
    internal static class BrickConnectivityCheck
    {
        public static BrickIntegrity Evaluate(ushort solidCount, DynamicBuffer<BrickSolidMask> mask,
            DynamicBuffer<BrickCells> dense, DynamicBuffer<BrickSparseCells> sparse)
        {
            var result = new BrickIntegrity { SolidCountAtCheck = solidCount };
            if (solidCount == 0) { result.State = BrickConnectivity.Unknown; return result; }
            if (solidCount == 1 || solidCount == 4096)
            {
                result.State = BrickConnectivity.Connected;
                for (int word = 0; word < 64; word++)
                    for (ulong bits = mask[word].Bits; bits != 0; bits &= bits - 1)
                        if (BrickWrite.Get(dense, sparse, word * 64 + math.tzcnt(bits)).IsInstance)
                        { result.HasInstanceCells = 1; return result; }
                return result;
            }
            var remaining = new NativeArray<uint>(256, Allocator.Temp);
            var parents = new NativeArray<int>(4096, Allocator.Temp);
            using var instances = new NativeHashMap<int, int>(16, Allocator.Temp);
            using var queue = new NativeList<int2>(Allocator.Temp);
            for (int row = 0; row < 256; row++) remaining[row] = (uint)(mask[row >> 2].Bits >> ((row & 3) * 16)) & 0xffff;
            int labels = 0, components = 0;
            for (int start = 0; start < 256; start++)
                while (remaining[start] != 0)
                {
                    int label = labels++; parents[label] = label; components++;
                    queue.Clear(); queue.Add(new int2(start, 1 << math.tzcnt(remaining[start])));
                    for (int head = 0; head < queue.Length; head++)
                    {
                        int row = queue[head].x; uint span = (uint)queue[head].y & remaining[row];
                        if (span == 0) continue;
                        // Fill X runs from the incoming seeds. A row boundary never wraps into another row.
                        uint previous;
                        do { previous = span; span |= ((span << 1) | (span >> 1)) & remaining[row]; } while (span != previous);
                        remaining[row] &= ~span;
                        for (uint bits = span; bits != 0; bits &= bits - 1)
                        {
                            var cell = BrickWrite.Get(dense, sparse, row * 16 + math.tzcnt(bits));
                            if (!cell.IsInstance) continue;
                            result.HasInstanceCells = 1;
                            if (instances.TryGetValue(cell.InstanceIndex, out int other))
                            {
                                int a = Root(parents, label), b = Root(parents, other);
                                if (a != b) { parents[math.max(a, b)] = math.min(a, b); components--; }
                            }
                            else instances.Add(cell.InstanceIndex, label);
                        }
                        int y = row & 15, z = row >> 4;
                        if (y > 0) Enqueue(row - 1, span, remaining, queue);
                        if (y < 15) Enqueue(row + 1, span, remaining, queue);
                        if (z > 0) Enqueue(row - 16, span, remaining, queue);
                        if (z < 15) Enqueue(row + 16, span, remaining, queue);
                    }
                }
            result.State = components == 1 ? BrickConnectivity.Connected : BrickConnectivity.Split;
            remaining.Dispose(); parents.Dispose();
            return result;
        }
        static int Root(NativeArray<int> parents, int label)
        {
            while (parents[label] != label) { parents[label] = parents[parents[label]]; label = parents[label]; }
            return label;
        }
        static void Enqueue(int row, uint span, NativeArray<uint> remaining, NativeList<int2> queue)
        { uint seeds = remaining[row] & span; if (seeds != 0) queue.Add(new int2(row, (int)seeds)); }
    }
}
