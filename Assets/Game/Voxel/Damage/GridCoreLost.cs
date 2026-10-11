using Unity.Entities;
using Unity.Mathematics;

namespace Game.Voxel.Damage
{
    /// <summary>Source event for ShipDestroyed (architecture §6.6); enriched with PlayerShip/Allegiance by the bridge.</summary>
    public struct GridCoreLost { public Entity Grid; public ulong PersistentId; public uint Tick; public Unity.Mathematics.float3 WorldPos; }
}
