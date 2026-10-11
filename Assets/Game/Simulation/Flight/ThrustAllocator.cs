using Unity.Collections;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>Implements SPEC-20 §4.5.</summary>
    public static class ThrustAllocator
    {
        /// Projected Gauss–Seidel (§5.4). u holds the warm start on entry and the result on exit (same length as actuators).
        /// Returns the final weighted residual norm². No allocation.
        public static float Solve(in ThrustProblem p, NativeArray<ThrustActuator> actuators, NativeArray<float> u)
        {
            float3 residual = Initialize(p, actuators, u);
            for (int sweep = 0; sweep < p.Sweeps; sweep++)
                for (int k = 0; k < actuators.Length; k++)
                    u[k] = Update(actuators[k], p.Weights, u[k], ref residual);
            float3 weighted = p.Weights * residual;
            return Dot(weighted, weighted);
        }

        // Internal steps let the required debug test check the physical residual after every production coordinate update.
        internal static float3 Initialize(in ThrustProblem p, NativeArray<ThrustActuator> actuators, NativeArray<float> u)
        {
            float3 residual = float3.zero;
            for (int k = 0; k < actuators.Length; k++)
            {
                var actuator = actuators[k];
                // A destroyed actuator can have a nonzero throttle from last tick, even when its new column is zero.
                u[k] = math.clamp(u[k], actuator.Min, actuator.Max);
                residual += actuator.Column * u[k];
            }
            return residual - p.Target;
        }

        // Accumulate the three float products in double and round once. Managed and Burst float reductions otherwise differ by an ULP (Q12).
        static float Dot(float3 a, float3 b) => (float)(((double)a.x * b.x + (double)a.y * b.y) + (double)a.z * b.z);

        internal static float Update(in ThrustActuator actuator, float3 weights, float u, ref float3 residual)
        {
            float3 weightedColumn = weights * actuator.Column;
            float denominator = Dot(weightedColumn, weightedColumn);
            if (denominator < 1e-12f) return u;
            float delta = -Dot(weightedColumn, weights * residual) / denominator;
            float next = math.clamp(u + delta, actuator.Min, actuator.Max);
            residual += actuator.Column * (next - u);
            return next;
        }
    }
}
