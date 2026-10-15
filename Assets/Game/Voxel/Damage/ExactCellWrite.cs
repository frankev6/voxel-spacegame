using Game.Voxel.Model;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    public enum ExactWriteKind : byte { Cell = 0, Instance = 1 }

    /// <summary>ADR-0117: one exact state write decided elsewhere (the co-op host): a cell set to a given value, or a block instance's
    /// health. <see cref="ExactCellApplySystem"/> applies it through the same CellWriter as damage, so every consequence (dirty bricks,
    /// neighbours, collider removal, brick deletion, CellRemoved for debris, sound and mass, functional entities) runs the same way.
    /// Writes for one grid are consecutive; within a grid, cells come before the instance writes that follow from them.</summary>
    public struct ExactCellWrite
    {
        public Entity Grid;
        public ExactWriteKind Kind;
        public CellRemovedReason Reason;   // Cell: why a solid cell became air (Destroyed, or DetachedBody when it left as a body)
        public int3 Cell;                  // Cell: grid cell coordinate
        public Cell Value;                 // Cell: the exact value
        public int Instance;               // Instance: index
        public float FunctionHp;           // Instance
        public ushort CellsAlive;          // Instance
        public BlockInstanceFlags Flags;   // Instance
    }
}
