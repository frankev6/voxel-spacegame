using Game.Voxel.Model;
using Game.Voxel.PhysicsBody;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Voxel.Damage.Jobs
{
    /// <summary>SPEC-13 §5.5 step 3: exact cell mass and parent point velocity, expressed at the chunk COM.</summary>
    internal struct IslandKinematics
    {
        public PhysicsMass Mass;
        public PhysicsVelocity Velocity;
        public float3 LocalCenter, WorldCenter;

        public static IslandKinematics Calculate(in StructureMassData cells, int3 cellMin, int3 cellMax,
            float cellSize, LocalTransform parent, PhysicsMass parentMass, PhysicsVelocity parentVelocity, float separation)
        {
            var mass = GridMassMath.CreateDynamicMass(cells, cellMin, cellMax, cellSize, false);
            float3 localCenter = mass.CenterOfMass;
            float3 center = parent.TransformPoint(localCenter);
            float3 direction = center - parent.TransformPoint(parentMass.CenterOfMass);
            direction.y = 0;
            direction = math.normalizesafe(direction, math.rotate(parent.Rotation, new float3(1, 0, 0)));
            float3 omega = math.rotate(math.mul(parent.Rotation, parentMass.InertiaOrientation), parentVelocity.Angular);
            var velocity = new PhysicsVelocity
            {
                Linear = DamageMath.VelocityAtPoint(parentVelocity, parentMass, parent, center) + direction * separation,
                Angular = math.rotate(math.inverse(math.mul(parent.Rotation, mass.InertiaOrientation)), omega)
            };
            // Chunk transform is at COM, unlike a grid transform. Both collider and visual cell origins use this rebasing.
            mass.CenterOfMass = float3.zero;
            return new IslandKinematics { Mass = mass, Velocity = velocity, LocalCenter = localCenter, WorldCenter = center };
        }
    }
}
