using System.Threading;
using Game.Core;
using Game.Data.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace Game.Simulation.Flight
{
    /// <summary>Removes vertical drift with a position bias, without writing transforms. Implements SPEC-20 §5.5.</summary>
    [BurstCompile]
    [WithAll(typeof(PlanarConstraint))]
    public partial struct PlanarImpulseJob : IJobEntity
    {
        public float Correction;
        // Shared diagnostic latch: only atomic compare-exchange writes it; one warning per system lifetime (Q16).
        [NativeDisableParallelForRestriction] public NativeReference<int> TiltReported;

        unsafe void Execute(ref PhysicsVelocity velocity, in PhysicsMass mass, in LocalTransform transform)
        {
            if (mass.InverseMass <= 0) return;
            float target = -Correction * transform.Position.y / SimTime.FixedTimestep;
            float impulse = (target - velocity.Linear.y) / mass.InverseMass;
            velocity.ApplyLinearImpulse(mass, new float3(0, impulse, 0));
            if ((math.abs(transform.Rotation.value.x) + math.abs(transform.Rotation.value.z) > .001f ||
                math.abs(velocity.Angular.x) + math.abs(velocity.Angular.z) > .0001f) &&
                Interlocked.CompareExchange(ref *TiltReported.GetUnsafePtr(), 1, 0) == 0)
            {
                FixedString128Bytes message = "FLT: planar body tilted";
                GameLog.Burst.Warn(message);
            }
        }
    }
}
