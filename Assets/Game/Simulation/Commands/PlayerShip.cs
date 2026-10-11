using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Tag on the player's current ship root (architecture §6.4). Added by the PlayerShip grid profile (ADR-0015). Implements SPEC-20 §4.1.</summary>
    public struct PlayerShip : IComponentData { }
}
