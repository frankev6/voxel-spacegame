using Unity.Entities;

namespace Game.Simulation.Spatial
{
    /// <summary>Tag: a grid left out of the spatial hash. For static, unarmed, unallied scenery (the Hollow's rock tiles): one
    /// sphere per 256 m tile would fill the AI's 127-proxy queries and make traffic swerve round tunnels it is flying through.
    /// Physics still collides with it; AI, targeting and explosions no longer see it.</summary>
    public struct SpatialHidden : IComponentData { }
}
