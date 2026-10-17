using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>On a mine device's root. Writer: MineSystem (authority); replicated (SPEC-91 §4.4).</summary>
    public struct MineState : IComponentData
    {
        public MinePhase Phase;
        public uint PhaseTick;      // tick the phase began
        public Entity TriggeredBy;
    }
}
