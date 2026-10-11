using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>One allocation problem (§5.4). Actuator columns are (Fx, Fz, τy) per unit throttle in the ship frame. Implements SPEC-20 §4.3.</summary>
    public struct ThrustProblem
    {
        public float3 Target;            // desired (Fx N, Fz N, τy N·m)
        public float3 Weights;           // per-row weights (wx, wz, wτ)
        public int Sweeps;
    }
}
