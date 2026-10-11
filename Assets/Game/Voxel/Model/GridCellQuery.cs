using Game.Voxel.Grid;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.5. Read-only ECS query using the shared grid lookup helper.</summary>
    public struct GridCellQuery : ICellSolidQuery
    {
        public GridCellReader Reader; public Entity Grid;
        public bool IsSolid(int3 cell) => Reader.IsSolid(Grid, cell);
        public bool BrickExists(int3 brick) => Reader.GetBrick(Grid, brick) != Entity.Null;
    }
}
