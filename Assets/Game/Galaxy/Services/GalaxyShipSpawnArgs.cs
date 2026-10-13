using Game.Core;
using Game.Galaxy.Data;
using Unity.Mathematics;

namespace Game.Galaxy.Services
{
    /// <summary>Implements SPEC-31 §4.4. Ship request in the local frame for the loaded star system.</summary>
    public struct GalaxyShipSpawnArgs
    {
        public ContentId Blueprint;
        public float3 Position;
        public quaternion Rotation;
        public float3 LinearVelocity;
        public Game.Simulation.Allegiance Allegiance;
        public bool WithAi;
        public ContentId AiProfile;
        public StarSystemId StarSystem;
        public ContentHash Encounter;
    }
}
