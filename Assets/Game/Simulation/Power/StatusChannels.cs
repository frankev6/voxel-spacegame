using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Singleton holding the status channels and the energy grant stream (ADR-0119). Created and
    /// disposed by StatusSystem.</summary>
    public struct StatusChannels : IComponentData
    {
        public Game.Data.Streams.SimEventChannel<StatModifierRequest> StatModifiers;
        public Game.Data.Streams.SimEventChannel<InhibitRequest> Inhibits;
        public Game.Data.Streams.SimEventChannel<EnergyGrantRequest> EnergyGrants;
    }
}
