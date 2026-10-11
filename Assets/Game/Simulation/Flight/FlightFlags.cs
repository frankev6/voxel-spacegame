using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Implements SPEC-20 §4.3.</summary>
    [System.Flags] public enum FlightFlags : byte { None = 0, Initialized = 1, Firing = 2, HulkIdled = 4 }
}
