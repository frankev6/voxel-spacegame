using Game.Voxel.Model;
using Unity.Collections;
using Unity.Entities;

namespace Game.Voxel.Grid
{
    /// <summary>SPEC-10 §4.6 and ADR-0023: representation-independent reads/writes with unchanged mask/revision semantics.</summary>
    public static class BrickWrite
    {
        public static Cell Get(DynamicBuffer<BrickCells> cells, DynamicBuffer<BrickSparseCells> sparse, int localIndex)
        {
            if (cells.Length != 0) return cells[localIndex].Value;
            int index = SparseCellStorage.Find(sparse.AsNativeArray(), 0, sparse.Length, localIndex);
            return index < 0 ? default : sparse[index].Value;
        }
        public static bool Set(ref Brick brick, DynamicBuffer<BrickCells> cells, DynamicBuffer<BrickSparseCells> sparse,
            DynamicBuffer<BrickSolidMask> mask, ref BrickDirty dirty, int localIndex, Cell value, uint revision)
        {
            if (value.IsAir) value = default;
            Cell old = Get(cells, sparse, localIndex);
            if (old.Equals(value)) return false;
            if (cells.Length == 0 && old.IsAir && !value.IsAir && sparse.Length == SparseCellStorage.Limit)
            {
                cells.Resize(BrickMath.CellsPerBrick, NativeArrayOptions.ClearMemory);
                foreach (var entry in sparse) cells[entry.Local] = new BrickCells { Value = entry.Value };
                sparse.Clear(); sparse.TrimExcess();
            }
            if (cells.Length != 0) cells[localIndex] = new BrickCells { Value = value };
            else
            {
                int index = SparseCellStorage.Find(sparse.AsNativeArray(), 0, sparse.Length, localIndex);
                if (value.IsAir) sparse.RemoveAt(index);
                else
                {
                    var entry = new BrickSparseCells { Local = (ushort)localIndex, Value = value };
                    if (index >= 0) sparse[index] = entry; else sparse.Insert(~index, entry);
                }
            }
            bool solidityChanged = old.IsAir != value.IsAir;
            if (solidityChanged)
            {
                int word = localIndex >> 6; ulong bit = 1UL << (localIndex & 63);
                mask[word] = new BrickSolidMask { Bits = value.IsAir ? mask[word].Bits & ~bit : mask[word].Bits | bit };
                brick.SolidCount = (ushort)(brick.SolidCount + (value.IsAir ? -1 : 1));
                dirty.Flags |= BrickDirtyFlags.Collider;
            }
            brick.ChangedRevision = revision; dirty.Flags |= BrickDirtyFlags.Mesh;
            if (solidityChanged || (old.Hp >> 6) != (value.Hp >> 6)) dirty.BorderMask |= BrickMath.BorderFaces(localIndex);
            return true;
        }
    }
}
