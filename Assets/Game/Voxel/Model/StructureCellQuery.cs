using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>Implements SPEC-10 §5.5. Read-only mask query; borrows the structure's native storage.</summary>
    public struct StructureCellQuery : ICellSolidQuery
    {
        public VoxelStructureData Data;
        public bool IsSolid(int3 cell)
        {
            if (!Data.IsCreated || !Data.BrickIndex.TryGetValue(BrickMath.BrickOf(cell), out int index)) return false;
            return Data.Masks[index].Get(BrickMath.LocalIndex(BrickMath.LocalOf(cell)));
        }
        public bool BrickExists(int3 brick) => Data.IsCreated && Data.BrickIndex.ContainsKey(brick);
    }
}
