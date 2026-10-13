using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. On every functional entity (registered with Module = None). Only writer: StatusSystem. Inhibited while SimClock.Tick < UntilTick.</summary>
    public struct Inhibited : IComponentData { public uint UntilTick; public byte Reason; }
}
