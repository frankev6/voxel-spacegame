using Game.Data.Definitions;
using Unity.Entities;

namespace Game.Galaxy.Services
{
    /// <summary>ADR-0087: creates render children during T1; the loader owns their links and lifecycle.</summary>
    public interface ICelestialVisualFactory
    {
        Entity Create(Entity anchor, BodyVisualDefinition definition, float radiusM);
    }
}
