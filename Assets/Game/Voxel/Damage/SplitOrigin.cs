using Unity.Entities;

namespace Game.Voxel.Damage
{
   // set by SplitSystem, and by SPEC-10 GridStowApi.Unstow for launches (ADR-0017)

    /// <summary>On new hulk roots and chunk bodies until SPEC-31 copies the parent's StarSystemMember (open question 4).</summary>
    public struct SplitOrigin : IComponentData { public Entity Parent; }
}
