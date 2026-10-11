using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Implements SPEC-20 §4.5.</summary>
    public struct ThrustActuator { public float3 Column; public float Min, Max; }
}
