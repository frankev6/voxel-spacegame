using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §5.5. Read-only use.</summary>
    public struct StructureCellQuery : ICellSolidQuery
    {
        public VoxelStructureData Data;
        public bool IsSolid(int3 cell) { throw new System.NotImplementedException("VOX-008"); }
        public bool BrickExists(int3 brick) { throw new System.NotImplementedException("VOX-008"); }
    }
}
