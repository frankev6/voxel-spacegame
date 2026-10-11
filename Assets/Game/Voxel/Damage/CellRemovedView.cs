using Unity.Collections;

namespace Game.Voxel.Damage
{
    /// <summary>Read-only view of this tick's CellRemoved list, grouped by grid in resolver order. Both arrays are deferred job
    /// arrays (NativeList.AsDeferredJobArray): use them only inside jobs scheduled on the handle BeginRemovedRead combined into
    /// state.Dependency, never on the main thread (SPEC-12 Q8).</summary>
    public struct CellRemovedView
    {
        [ReadOnly] public NativeArray<CellRemoved> Cells;
        [ReadOnly] public NativeArray<GridRemovedRange> Grids;
    }
}
