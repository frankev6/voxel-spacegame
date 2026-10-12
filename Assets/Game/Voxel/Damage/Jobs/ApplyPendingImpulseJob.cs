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
    /// <summary>SPEC-13 §5.2: next-tick impulses; PendingImpulse remains resolver-owned.</summary>
    [BurstCompile] internal partial struct ApplyPendingImpulseJob : IJobEntity
    {
        public uint Tick;
        [ReadOnly] public ComponentLookup<PlanarConstraint> Planar;
        void Execute(Entity entity, ref PhysicsVelocity velocity, in PhysicsMass mass, in LocalTransform transform, in PendingImpulse pending)
        {
            if (Tick == 0 || pending.Tick != Tick - 1) return;
            float3 linear = pending.LinearWorld, angular = pending.AngularWorld;
            if (Planar.HasComponent(entity)) { linear.y = 0; angular.x = angular.z = 0; }
            velocity.ApplyLinearImpulse(mass, linear);
            velocity.Angular += math.rotate(math.inverse(math.mul(transform.Rotation, mass.InertiaOrientation)), angular) * mass.InverseInertia;
        }
    }
}
