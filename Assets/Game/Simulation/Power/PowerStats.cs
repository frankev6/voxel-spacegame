using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Power totals of alive blocks (feature stats, ADR-0014). Only writer: ShipStatsAggregateSystem (power job).</summary>
    public struct PowerStats : IComponentData
    {
        public float Generation;        // E/s = Σ PowerSource rows
        public float Capacity;          // E = Σ PowerStorage rows
        public float DrawAtFullDemand;  // E/s = Σ consumer Draw
        public int AliveSourceCount;    // blocks with a PowerSource or PowerStorage module
        public uint SourceRevision;     // ShipStats.FunctionalRevision these totals were built from
    }
}
