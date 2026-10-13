using Unity.Entities;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.3. Requested shield state. Only writer: ShieldToggleEffectSystem (SPEC-24). Spawned as RequestedOn = 1.</summary>
    public struct ShieldControl : IComponentData { public byte RequestedOn; }
}
