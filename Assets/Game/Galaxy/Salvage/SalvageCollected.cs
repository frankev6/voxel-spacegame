using Unity.Entities;

namespace Game.Galaxy.Salvage
{
    /// <summary>QA-001: added by SalvageService when a wreck's cargo is taken. SalvagePullSystem streams the wreck's surface
    /// cubes into <see cref="Collector"/> in staggered waves, then requests the normal despawn.</summary>
    public struct SalvageCollected : IComponentData { public Entity Collector; public float Elapsed; }
}
