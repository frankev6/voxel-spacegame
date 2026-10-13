using Unity.Entities;
using Game.Galaxy.Data;
using Game.Data.Definitions;

namespace Game.Galaxy.Bodies
{
    /// <summary>Implements SPEC-31 §4.2. BodyOrbit contract. Created by StarSystemLoader for analytic anchors.</summary>
    public struct BodyOrbit : IComponentData
    {
        public OrbitElements Orbit;
        public OrbitElements ParentOrbit;
    }
}
