using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Commands
{
    /// <summary>Implements SPEC-20 §4.1.</summary>
    [System.Flags] public enum ShipCommandFlags : byte { None = 0, Boost = 1, Brake = 2 }   // add at the end only
}
