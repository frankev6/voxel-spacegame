using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. On every functional entity of a power-consuming module (registered by PowerRegistration.RegisterConsumer, so it starts
    /// zeroed). Only writer: PowerInitSystem.</summary>
    public struct PowerLink : IComponentData
    {
        public short ConsumerCount;     // PowerRequest elements bound to PowerConsumer entries; 0 = not a consumer
        public byte Bound;              // 0 = new entity (spawn, rebuild or edit), 1 = bound by PowerInitSystem
        public byte Reserved;
    }
}
