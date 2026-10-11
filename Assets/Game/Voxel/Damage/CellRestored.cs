using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>A cell that Place added this tick (published like CellRemoved; SPEC-12 adds its mass).</summary>
    public struct CellRestored { public Entity Grid; public int3 Cell; public ushort Block; public byte Color; }
}
