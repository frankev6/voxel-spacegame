using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Added to a grid being dissolved. Writer: ShipDeathSystem.</summary>
    public struct GridDissolving : IComponentData { public uint StartTick; public int NextSlot; public int Emitted; public int Stride; }
}
