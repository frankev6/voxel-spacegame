using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Voxel.Model
{
    public sealed partial class VoxelStructure
    {
        // Frozen pre-VOX-022 ingestion for differential correctness/performance tests only.
        internal void SetBrickDenseLegacy(int3 coord, NativeArray<Cell> cells4096)
        {
            RequireMutable();
            if (cells4096.Length != BrickMath.CellsPerBrick) throw new ArgumentException("A brick must contain 4096 cells", nameof(cells4096));
            var mask = new BrickMaskBits(); int count = 0;
            for (int i = 0; i < cells4096.Length; i++) if (!cells4096[i].IsAir) { mask.Set(i, true); count++; }
            if (count == 0) { RemoveBrick(coord); return; }
            bool exists = data.BrickIndex.TryGetValue(coord, out int index);
            var record = exists ? data.Bricks[index] : new BrickRecord { Coord = coord, Page = -1 };
            if (exists)
            {
                bool same = true;
                for (int i = 0; i < cells4096.Length && same; i++) same = Read(record, i).Equals(cells4096[i].IsAir ? default : cells4096[i]);
                if (same) return;
            }
            if (count <= SparseCellStorage.Limit)
            {
                if (record.Page >= -1) { ReleasePage(record); record.Page = SparseCellStorage.EncodePage(TakeSparsePage()); }
                int start = SparseCellStorage.PageIndex(record.Page) * SparseCellStorage.Limit, at = 0;
                for (int i = 0; i < cells4096.Length; i++)
                    if (!cells4096[i].IsAir) data.SparsePages[start + at++] = new BrickSparseCells { Local = (ushort)i, Value = cells4096[i] };
                for (int i = at; i < SparseCellStorage.Limit; i++) data.SparsePages[start + i] = default;
            }
            else
            {
                if (record.Page < 0) { ReleasePage(record); record.Page = TakePage(); }
                for (int i = 0; i < cells4096.Length; i++) data.CellPages[record.Page * BrickMath.CellsPerBrick + i] = cells4096[i].IsAir ? default : cells4096[i];
            }
            record.Uniform = default; record.SolidCount = (ushort)count; record.ChangedRevision = Changed();
            if (exists) { data.Bricks[index] = record; data.Masks[index] = mask; }
            else { data.BrickIndex.Add(coord, data.Bricks.Length); data.Bricks.Add(record); data.Masks.Add(mask); }
        }
    }
}
