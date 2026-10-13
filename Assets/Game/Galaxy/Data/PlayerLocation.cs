using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.6.</summary>
    public struct PlayerLocation
    {
        public StarSystemId StarSystem;
        public Unity.Mathematics.double3 PositionM;            // star-system frame (UniversePosition)
        public float YawRad;
    }
}
