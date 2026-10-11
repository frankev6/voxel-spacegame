using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Singleton: counters already turned into ShipCommandEdges. Only writer: PlayerCommandApplySystem (created in its OnCreate). Implements SPEC-20 §4.1.</summary>
    public struct PlayerEdgeConsumed : IComponentData { public PlayerEdgeCounters Value; }
}
