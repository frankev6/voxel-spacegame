using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>ADR-0117: a ship a player flies, on every machine: the host's own ship (slot 0) and each co-op guest's ship (its slot).
    /// <see cref="PlayerShip"/> stays the one THIS machine flies (input, camera, HUD). Simulation that serves players (interaction,
    /// salvage reach) iterates pilots, so a guest's ship gets the same code as the host's. Added by <see cref="PilotSetupSystem"/> to the
    /// local player's ship, by the co-op session to guests' ships on the host, and from spawn headers on guests.</summary>
    public struct Pilot : IComponentData { public byte Slot; }

    /// <summary>Interact presses this pilot made, counted once per fixed tick from its ShipCommand edges (local input and a remote
    /// pilot's commands alike). Consumers keep their own count and act on the difference. Only writer: PilotInteractCountSystem.</summary>
    public struct PilotInteractCount : IComponentData { public byte Value; }
}
