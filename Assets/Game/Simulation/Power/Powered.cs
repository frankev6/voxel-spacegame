using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. Enable bit on functional entities: the block has power this tick. Only writer: PowerAllocationSystem.</summary>
    public struct Powered : IComponentData, IEnableableComponent { }
}
