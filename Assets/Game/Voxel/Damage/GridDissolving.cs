using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Added to a grid being dissolved. Writer: ShipDeathSystem. NextSlot is the packed next cell cursor (slot &lt;&lt; 12 | local), SPEC-13 Q35.</summary>
    public struct GridDissolving : IComponentData { public uint StartTick; public int NextSlot; public int Emitted; public int Stride; }
}
