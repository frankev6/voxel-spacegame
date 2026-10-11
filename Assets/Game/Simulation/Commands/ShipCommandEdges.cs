using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>One-tick action requests (decision 8). Add at the end only. Implements SPEC-20 §4.1.</summary>
    [System.Flags] public enum ShipCommandEdges : ushort
    {
        None = 0, Interact = 1, Ability1 = 2, Ability2 = 4, Ability3 = 8, Ability4 = 16,
        TargetNext = 32, TargetUnderCursor = 64, TargetClear = 128, AutofireToggle = 256,
    }
}
