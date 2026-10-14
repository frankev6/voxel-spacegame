using Unity.Entities;

namespace Game.Galaxy.Salvage
{
    /// <summary>QA-001: added by SalvageService with the despawn request; SalvagePullSystem turns the wreck's exposed
    /// cells into debris flying into <see cref="Collector"/> before GridSpawnSystem removes the grid.</summary>
    public struct SalvageCollected : IComponentData { public Entity Collector; }
}
