using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Tag: the grid takes no damage (indestructible bodies, SPEC-30 Q15; SPEC-31 adds it for `destructible == false`).
    /// DamageResolveSystem drops events on it in Gather (no cells, no knockback); projectiles and beams still stop on it.</summary>
    public struct DamageImmune : IComponentData { }
}
