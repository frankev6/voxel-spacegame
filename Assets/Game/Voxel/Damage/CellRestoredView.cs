using Unity.Collections;

namespace Game.Voxel.Damage
{
    /// <summary>This tick's CellRestored list grouped by grid (deferred job arrays, like CellRemovedView).</summary>
    public struct CellRestoredView { [ReadOnly] public NativeArray<CellRestored> Cells; [ReadOnly] public NativeArray<GridRemovedRange> Grids; }
}
