using Game.Galaxy.Data;
using Unity.Entities;

namespace Game.Galaxy.Components
{
    /// <summary>Enableable, on the FrameOrigin entity. Written only by GalaxyService.RequestOriginReset; consumed (disabled) by OriginRebaseSystem. Implements SPEC-30 §4.7.</summary>
    public struct OriginRebaseRequest : IComponentData, IEnableableComponent
    {
        public Unity.Mathematics.double3 NewOrigin;
        public StarSystemId StarSystem;
        public uint Serial;                                    // monotonically increasing, starts at 1
        public byte PlacePlayer;                               // 1: ShiftJob places the PlayerShip root (SPEC-31 Q2, SPEC-32)
        public PlayerPlacement Placement;                      // star-system frame; read only when PlacePlayer == 1
    }
}
