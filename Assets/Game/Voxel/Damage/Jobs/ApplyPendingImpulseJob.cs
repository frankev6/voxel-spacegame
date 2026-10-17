using Game.Data.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.2: next-tick impulses; PendingImpulse remains resolver-owned. Records the velocity change in
    /// ExternalPush (SPEC-91 §5.1 step 3), which ApplyRequestedImpulseJob then adds to in the same tick.</summary>
    [BurstCompile] internal partial struct ApplyPendingImpulseJob : IJobEntity
    {
        public uint Tick;
        [ReadOnly] public ComponentLookup<PlanarConstraint> Planar;
        // Each entity writes only its own row.
        [NativeDisableParallelForRestriction] public ComponentLookup<ExternalPush> Pushes;
        void Execute(Entity entity, ref PhysicsVelocity velocity, in PhysicsMass mass, in LocalTransform transform, in PendingImpulse pending)
        {
            if (Tick == 0 || pending.Tick != Tick - 1) return;
            float3 linear = pending.LinearWorld, angular = pending.AngularWorld;
            if (Planar.HasComponent(entity)) { linear.y = 0; angular.x = angular.z = 0; }
            velocity.ApplyLinearImpulse(mass, linear);
            quaternion inertia = math.mul(transform.Rotation, mass.InertiaOrientation);
            float3 dw = math.rotate(math.inverse(inertia), angular) * mass.InverseInertia;
            velocity.Angular += dw;
            if (Pushes.HasComponent(entity))
                Pushes[entity] = new ExternalPush { LinearDv = linear * mass.InverseMass, YawDv = math.rotate(inertia, dw).y, Tick = Tick };
        }
    }
}
