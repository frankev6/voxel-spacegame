using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Press counters (SPEC-03 decision 4). Wrap at 255; only differences matter. Implements SPEC-20 §4.1.</summary>
    public struct PlayerEdgeCounters
    {
        public byte Interact, Ability1, Ability2, Ability3, Ability4, TargetNext, TargetUnderCursor, TargetClear, AutofireToggle;
        public byte Cruise;              // SPEC-03 Flight/Cruise; not a ship edge: consumed only by SPEC-32 TravelService (M4) as a
                                         // variable-rate edge. Present from M0 so the struct (and SPEC-71 replays) never change size
    }
}
