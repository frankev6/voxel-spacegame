using Game.Data.Combat;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 power stream (ADR-0119 decision 7): add energy to a ship's storage, capped by its capacity. Producers: energy
    /// canisters (PickupSystem, HAZ-019). The channel lives in StatusChannels; PowerAllocationSystem consumes it before allocating.</summary>
    public struct EnergyGrantRequest
    {
        public Entity Ship;
        public float Energy;
        public ProducerKind Producer;
        public uint Sequence;
    }
}
