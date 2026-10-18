using Unity.Entities;

namespace Game.Simulation.Spatial
{
    /// <summary>Tag: static, unarmed, unallied scenery (the Hollow's rock tiles). Its proxy carries <see cref="ProxyFlags.Scenery"/>:
    /// hit tests (projectiles, beams, explosions) see it, so shots stop at the rock; the AI's and the player's targeting queries skip
    /// it, since one sphere per 256 m tile would fill their 127-proxy queries and make traffic swerve round tunnels it is flying
    /// through.</summary>
    public struct SpatialScenery : IComponentData { }
}
