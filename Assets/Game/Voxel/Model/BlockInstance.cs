using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Model
{

    /// <summary>Implements SPEC-10 §4.1. A multi-cell or functional block, 32 bytes; buffer element on the grid root.</summary>
    [InternalBufferCapacity(0)]
    public struct BlockInstance : IBufferElementData
    {
        public ushort Block;          // BlockIndex; 0 = free slot (tombstone)
        public byte Orient;           // 0..23
        public byte Color;
        public int3 Origin;           // grid cell of footprint cell (0,0,0)
        public float FunctionHp;      // absolute HP, starts at BlockDef.FunctionHp; runtime writer: DamageResolveSystem only
        public Entity Functional;     // functional entity, or Entity.Null (Structural/Prop kinds, or outside ECS)
        public ushort CellsAlive;     // footprint cells still solid; starts at BlockDef.Cells.Length
        public BlockInstanceFlags Flags;
        public byte Reserved;
    }
}
