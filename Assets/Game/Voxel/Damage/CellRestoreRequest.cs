using Game.Data.Combat;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
   // add at end only

    /// <summary>Request to restore cells (ADR-0016): repair beams, drones, station repair, narrative repair. Consumed by DamageResolveSystem (§5.3.1 step 3b).</summary>
    public struct CellRestoreRequest
    {
        public Entity Grid;
        public CellRestoreMode Mode;
        public int3 CellMin, CellMax;  // Heal: inclusive box in grid cells; Place: CellMin is the cell (CellMax ignored)
        public float HpFraction;       // Heal: fraction of each cell's max HP (and of each touched live instance's FunctionHp) to add, (0, 1]
        public ushort Block;           // Place: BlockIndex of a palette block (single-cell, !RequiresInstance)
        public byte Orient, Color;     // Place
        public ProducerKind Producer;
        public uint Sequence;
    }
}
