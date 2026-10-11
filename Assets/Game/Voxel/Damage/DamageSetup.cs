using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Components SPEC-10 puts on grid roots and bricks at spawn (open question 1).</summary>
    public static class DamageSetup
    {
        public static ComponentType[] GridRootComponents() => new[] { ComponentType.ReadWrite<GridDamageStats>(), ComponentType.ReadWrite<PendingImpulse>(), ComponentType.ReadWrite<GridIntegrityState>(), ComponentType.ReadWrite<IntegritySeed>() };  // GridDamageStats, PendingImpulse, GridIntegrityState, IntegritySeed
        public static ComponentType[] BrickComponents() => new[] { ComponentType.ReadWrite<BrickIntegrity>() };     // BrickIntegrity
    }
}
