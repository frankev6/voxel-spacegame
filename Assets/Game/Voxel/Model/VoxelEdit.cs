using Unity.Mathematics;

namespace Game.Voxel.Model
{
    /// <summary>ADR-0082: source-structure batch operations; all writes remain in Game.Voxel.</summary>
    public enum VoxelEditKind : byte { PlaceBlock, RemoveBlock, SetCellHp, ClearCell, SetFunctionHp }
    public struct VoxelEdit
    {
        public VoxelEditKind Kind;
        public ushort Block;
        public byte Orient, Color, Hp;
        public int3 Cell;
        public float FunctionHp;
    }
}
