using System.Collections.Generic;
using Game.Data.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Extensions;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.2 step 1b: ordered requests use the same exclusive impulse writer as collision knockback.</summary>
    [BurstCompile] internal unsafe struct ApplyRequestedImpulseJob : IJob
    {
        [ReadOnly] public NativeList<ImpulseRequest> Requests;
        public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<PhysicsMass> Masses;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<PlanarConstraint> Planar;
        public ComponentLookup<ExternalPush> Pushes;   // summed per root this tick (SPEC-91 §5.1 step 3)
        public uint Tick;
        [NativeDisableUnsafePtrRestriction] public DamageStreamsData* Data;

        struct Entry { public ImpulseRequest Request; public int Order; }
        struct Order : IComparer<Entry>
        {
            public int Compare(Entry a, Entry b)
            {
                int c = a.Request.Body.Index.CompareTo(b.Request.Body.Index); if (c != 0) return c;
                c = ((byte)a.Request.Producer).CompareTo((byte)b.Request.Producer); if (c != 0) return c;
                c = a.Request.Sequence.CompareTo(b.Request.Sequence); return c != 0 ? c : a.Order.CompareTo(b.Order);
            }
        }
        public void Execute()
        {
            var entries = new NativeArray<Entry>(Requests.Length, Allocator.Temp);
            for (int i = 0; i < Requests.Length; i++) entries[i] = new Entry { Request = Requests[i], Order = i };
            entries.Sort(new Order());
            for (int i = 0; i < entries.Length; i++)
            {
                var request = entries[i].Request; Entity body = request.Body;
                bool atCentre = (request.Flags & ImpulseRequestFlags.AtCenterOfMass) != 0;
                if (!math.all(math.isfinite(request.WorldImpulse)) || !math.all(math.isfinite(request.WorldPoint)) ||
                    !Velocities.HasComponent(body) || !Masses.HasComponent(body) || !atCentre && !Transforms.HasComponent(body))
                { Data->Current.ImpulsesDropped++; continue; }
                var velocity = Velocities[body]; var mass = Masses[body];
                float3 linear = request.WorldImpulse, angular = float3.zero;
                var pose = atCentre ? LocalTransform.Identity : Transforms[body];
                if (!atCentre) angular = math.cross(request.WorldPoint - pose.TransformPoint(mass.CenterOfMass), linear);
                if (Planar.HasComponent(body)) { linear.y = 0; angular.x = angular.z = 0; }
                velocity.ApplyLinearImpulse(mass, linear);
                float yawDv = 0;
                if (!atCentre)
                {
                    quaternion inertia = math.mul(pose.Rotation, mass.InertiaOrientation);
                    float3 dw = math.rotate(math.inverse(inertia), angular) * mass.InverseInertia;
                    velocity.Angular += dw; yawDv = math.rotate(inertia, dw).y;
                }
                Velocities[body] = velocity; Data->Current.ImpulsesApplied++;
                if (Pushes.HasComponent(body))
                {
                    var push = Pushes[body];
                    if (push.Tick != Tick) push = new ExternalPush { Tick = Tick };
                    push.LinearDv += linear * mass.InverseMass; push.YawDv += yawDv;
                    Pushes[body] = push;
                }
            }
            entries.Dispose();
        }
    }
}
