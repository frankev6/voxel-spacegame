using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6. Burst; the cell write path for SPEC-13 and GridEditRequest.</summary>
    public static class BrickWrite
    {
        public static Cell Get(DynamicBuffer<BrickCells> cells, int localIndex) => cells[localIndex].Value;
        /// <summary>Writes one cell; maintains mask, SolidCount, ChangedRevision and dirty flags. Returns true if anything changed.</summary>
        public static bool Set(ref Brick brick, DynamicBuffer<BrickCells> cells, DynamicBuffer<BrickSolidMask> mask, ref BrickDirty dirty,
                               int localIndex, Cell value, uint revision)
        {
            if (value.IsAir) value = default;
            Cell old = cells[localIndex].Value;
            if (old.Equals(value)) return false;
            bool solidityChanged = old.IsAir != value.IsAir;
            cells[localIndex] = new BrickCells { Value = value };
            if (solidityChanged)
            {
                int word = localIndex >> 6;
                ulong bit = 1UL << (localIndex & 63);
                mask[word] = new BrickSolidMask { Bits = value.IsAir ? mask[word].Bits & ~bit : mask[word].Bits | bit };
                brick.SolidCount = (ushort)(brick.SolidCount + (value.IsAir ? -1 : 1));
                dirty.Flags |= BrickDirtyFlags.Collider;
            }
            brick.ChangedRevision = revision;
            dirty.Flags |= BrickDirtyFlags.Mesh;
            if (solidityChanged || (old.Hp >> 6) != (value.Hp >> 6))
                dirty.BorderMask |= BrickMath.BorderFaces(localIndex);
            return true;
        }
    }
}
