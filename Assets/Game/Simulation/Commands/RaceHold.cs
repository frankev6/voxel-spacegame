using Unity.Entities;

namespace Game.Simulation.Commands
{
    /// <summary>On pilot roots while they must hold still (race lobby and countdown, SPEC-92 §4.2). Writer: RaceSystem (Galaxy); the
    /// pilot command conversion zeroes thrust, turn and fire while it is enabled. Lives here so Simulation can read it.</summary>
    public struct RaceHold : IComponentData, IEnableableComponent { }
}
