using Game.Galaxy.Data;
using Unity.Entities;

namespace Game.Galaxy.Components
{
    /// <summary>Where the player ship is put by a placement request (star-system frame, yaw only, ADR-0003). Implements SPEC-30 §4.7.</summary>
    public struct PlayerPlacement { public Unity.Mathematics.double3 PositionM; public float YawRad; }
}
