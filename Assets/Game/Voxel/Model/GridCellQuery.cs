using Game.Voxel.Grid;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.5.</summary>
    public struct GridCellQuery : ICellSolidQuery
    {
        public GridCellReader Reader; public Entity Grid;
        public bool IsSolid(int3 cell) { throw new System.NotImplementedException("VOX-008"); }
        public bool BrickExists(int3 brick) { throw new System.NotImplementedException("VOX-008"); }
    }
}
