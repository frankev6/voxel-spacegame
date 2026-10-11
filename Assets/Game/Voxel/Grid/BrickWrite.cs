using Game.Voxel.Model;
using Unity.Entities;

namespace Game.Voxel.Grid
{

    /// <summary>Implements SPEC-10 §4.6. Burst; the cell write path for SPEC-13 and GridEditRequest.</summary>
    public static class BrickWrite
    {
        public static Cell Get(DynamicBuffer<BrickCells> cells, int localIndex) { throw new System.NotImplementedException("VOX-012"); }
        /// <summary>Writes one cell; maintains mask, SolidCount, ChangedRevision and dirty flags. Returns true if anything changed.</summary>
        public static bool Set(ref Brick brick, DynamicBuffer<BrickCells> cells, DynamicBuffer<BrickSolidMask> mask, ref BrickDirty dirty,
                               int localIndex, Cell value, uint revision) { throw new System.NotImplementedException("VOX-012"); }
    }
}
