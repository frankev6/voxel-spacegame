using Game.Data.Combat;
using Unity.Collections;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Product of active ShipStatModifier multipliers per StatId (1 if none). Only writer: ShipStatsAggregateSystem.</summary>
    public struct ShipStatMultipliers : IComponentData
    {
        public FixedList64Bytes<float> Values;   // index = (int)StatId; length = highest StatId + 1; filled with 1 at init
        public float Get(StatId id) => (int)id < Values.Length ? Values[(int)id] : 1f;
    }
}
