using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>Tag: collisions take no cells from the grid (SPEC-93: the den's stone, the station core, the Lantern's doors). Weapons,
    /// explosions and digging still damage it; EmitRamDamageJob emits no ram damage onto it. The other body in the contact still takes
    /// its share, so a ship that rams the rock is hurt and the rock is not; under EmitRamDamageJob.ScrapeSpeedMs of closing speed
    /// the contact is a scrape and neither side is hurt.</summary>
    public struct RamImmune : IComponentData { }
}
