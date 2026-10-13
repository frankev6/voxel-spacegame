using Game.Data.Combat;
using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Active stat modifier on the grid root. Only writer: StatusSystem (§5.3b, ADR-0016), from StatModifierRequests.</summary>
    [InternalBufferCapacity(4)]
    public struct ShipStatModifier : IBufferElementData   // 12 bytes
    {
        public StatId Stat;
        public Game.Data.Combat.ProducerKind Producer;   // who asked: Ability, Status, Environment…
        public ushort SourceId;         // the producer's key (abilities: slot index)
        public float Multiplier;        // > 0
        public uint UntilTick;          // removed when SimClock.Tick ≥ UntilTick; uint.MaxValue = until cancelled
    }
}
