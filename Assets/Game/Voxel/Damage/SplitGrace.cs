using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>On new hulk roots and chunk bodies: contacts with Other are ignored until UntilTick (SPEC-12 honours it).</summary>
    public struct SplitGrace : IComponentData { public Entity Other; public uint UntilTick; }
}
