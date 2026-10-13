using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Shield totals of alive emitters (feature stats). Only writer: ShipStatsAggregateSystem (power job).</summary>
    public struct ShieldStats : IComponentData
    {
        public float Capacity, Regen, RechargeDelay, Draw;   // Σ capacity, Σ regen/s, min delay (s), Σ draw (E/s)
        public int AliveEmitterCount;
        public uint SourceRevision;
    }
}
