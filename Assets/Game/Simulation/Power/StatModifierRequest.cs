using Game.Data.Combat;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Request to set, refresh or cancel a stat modifier (ADR-0016). Upsert key (Root, Producer, SourceId, Stat);
    /// Multiplier == 1 or UntilTick ≤ current tick cancels.</summary>
    public struct StatModifierRequest
    {
        public Entity Root; public StatId Stat; public Game.Data.Combat.ProducerKind Producer; public ushort SourceId;
        public float Multiplier; public uint UntilTick; public uint Sequence;
    }
}
