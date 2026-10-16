using Unity.Entities;

namespace Game.Galaxy.Salvage
{
    /// <summary>QA-001: added by SalvageService when a wreck's cargo is taken (ADR-0117: the authority's decision, replicated to co-op
    /// guests). SalvagePullSystem streams the wreck's surface cubes into <see cref="Collector"/> in staggered waves on every machine;
    /// SalvageDespawnSystem (authority) then requests the normal despawn.</summary>
    public struct SalvageCollected : IComponentData { public Entity Collector; public float Elapsed; }
}
