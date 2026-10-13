using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.2. Request to switch blocks off (EMP, ion, hacking; ADR-0016). Target = a functional entity, or a grid root = all its blocks.</summary>
    public struct InhibitRequest
    {
        public Entity Target; public uint DurationTicks; public byte Reason; public Game.Data.Combat.ProducerKind Producer; public uint Sequence;
    }
}
