using Unity.Entities;

namespace Game.Voxel.Damage
{
    /// <summary>SPEC-13 §5.6 core floor: tag on a ship whose live cells have reached its core's floor (CoreParams.Floor) at least
    /// once. Only an armed ship malfunctions when it falls below the floor, so a grid built undersized for its core (a test rig,
    /// a device) is never wrecked by it; the Blueprint Lab refuses such designs. Writer: ShipDeathSystem (authority). Not saved:
    /// a loaded ship re-arms on its first check while it is at or above its floor.</summary>
    public struct CoreFloorArmed : IComponentData { }
}
