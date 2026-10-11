using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>One cell left a grid this tick. Output of the resolver. Implements SPEC-13 §4.2.</summary>
    public struct CellRemoved
    {
        public Entity Grid;
        public int3 Cell;
        public ushort Block;           // BlockIndex
        public byte Color;
        public float3 WorldPos;        // local-frame world position of the cell centre
        public float3 WorldVel;        // parent rigid velocity at WorldPos (v + ω×r)
        public float3 Kick;            // extra debris velocity; debris velocity = WorldVel + Kick
        // ---- SPEC-13 additions ----
        public CellRemovedReason Reason;
        public byte Orient;
        public int InstanceIndex;      // BlockInstance index, -1 for palette cells
    }
}
