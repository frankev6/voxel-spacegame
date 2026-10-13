using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.1. Per-ship energy state on the grid root. Only writer: PowerAllocationSystem (and PowerInitSystem at init).</summary>
    public struct ShipPower : IComponentData
    {
        public float Stored;            // E, 0..Capacity
        public float Capacity;          // E = Σ alive core.PowerStorage + capacitor.PowerStorage
        public float Generation;        // E/s = Σ alive core.PowerGeneration + reactor.PowerGeneration; 0 on hulks
        public float DemandPerSecond;   // E/s requested this tick (HUD)
        public float GrantedPerSecond;  // E/s granted this tick (HUD)
        public uint AbilityGrantMask;   // bit i = AbilitySlot i's pending one-shot cost was granted this tick
        public uint AbilityGrantTick;   // SimClock.Tick the mask belongs to
        public ShipPowerFlags Flags;
    }
}
