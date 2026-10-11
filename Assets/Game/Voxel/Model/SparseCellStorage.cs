using Unity.Collections;

namespace Game.Voxel.Model
{
    /// <summary>Shared deterministic source-record reads and sorted sparse search (ADR-0023).</summary>
    internal static class SparseCellStorage
    {
        internal const int Limit = 1024;
        internal static int PageIndex(int encodedPage) => -2 - encodedPage;
        internal static int EncodePage(int index) => -2 - index;
        internal static int Find(NativeArray<BrickSparseCells> entries, int start, int count, int local)
        {
            int lo = 0, hi = count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (entries[start + mid].Local < local) lo = mid + 1; else hi = mid;
            }
            return lo < count && entries[start + lo].Local == local ? lo : ~lo;
        }
        internal static Cell Read(BrickRecord record, NativeArray<Cell> dense, NativeArray<BrickSparseCells> sparse, int local)
        {
            if (record.Page == -1) return record.Uniform;
            if (record.Page >= 0) return dense[record.Page * BrickMath.CellsPerBrick + local];
            int start = PageIndex(record.Page) * Limit, index = Find(sparse, start, record.SolidCount, local);
            return index < 0 ? default : sparse[start + index].Value;
        }
        internal static void Expand(BrickRecord record, NativeArray<Cell> dense, NativeArray<BrickSparseCells> sparse, NativeArray<Cell> output)
        {
            if (record.Page >= 0)
            { NativeArray<Cell>.Copy(dense, record.Page * BrickMath.CellsPerBrick, output, 0, BrickMath.CellsPerBrick); return; }
            for (int i = 0; i < BrickMath.CellsPerBrick; i++) output[i] = record.Page == -1 ? record.Uniform : default;
            if (record.Page == -1) return;
            int start = PageIndex(record.Page) * Limit;
            for (int i = 0; i < record.SolidCount; i++) { var entry = sparse[start + i]; output[entry.Local] = entry.Value; }
        }
    }
}
