using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Implements SPEC-20 §4.5.</summary>
    public static class ThrustAllocator
    {
        /// Projected Gauss–Seidel (§5.4). u holds the warm start on entry and the result on exit (same length as actuators).
        /// Returns the final weighted residual norm². No allocation.
        public static float Solve(in ThrustProblem p, Unity.Collections.NativeArray<ThrustActuator> actuators, Unity.Collections.NativeArray<float> u) => throw new System.NotImplementedException("FLT-004");
    }
}
