using Game.Core.Systems;
using Game.Data.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Flight
{
    /// <summary>RACE-016 (owner: "hitting an edge still makes you spin like crazy"): a flown ship's yaw rate after the physics step is
    /// held to SpinFactor × its own turn cap. A light hull clipping a voxel post or a wreck at 190 m/s took 22 rad/s from one contact
    /// and needed 3 s to recover; now the hit is a hard jolt the pilot can fly out of. Collisions still move, damage and deflect it.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup), OrderFirst = true)]
    public partial struct SpinLimitSystem : ISystem
    {
        internal const float SpinFactor = 1.6f, MinSpin = 1.5f;   // rad/s floor for ships with a tiny turn cap

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (velocity, mass, pose, flight, entity) in SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<PhysicsMass>, RefRO<LocalTransform>, RefRO<FlightState>>().WithEntityAccess())
            {
                if (mass.ValueRO.InverseMass <= 0) continue;
                var inertia = math.mul(pose.ValueRO.Rotation, mass.ValueRO.InertiaOrientation);
                float3 world = math.rotate(inertia, velocity.ValueRO.Angular);   // Angular is in inertia space
                float cap = math.max(MinSpin, flight.ValueRO.YawRateCap * SpinFactor);
                // ADR-0123: a 3D-flying ship can also tumble, so its whole spin is held to the cap.
                if (SystemAPI.HasComponent<Flight3D>(entity) && SystemAPI.IsComponentEnabled<Flight3D>(entity))
                {
                    float spin = math.length(world);
                    if (spin > cap) velocity.ValueRW.Angular = math.rotate(math.inverse(inertia), world * (cap / spin));
                    continue;
                }
                if (math.abs(world.y) <= cap) continue;
                world.y = math.sign(world.y) * cap;
                velocity.ValueRW.Angular = math.rotate(math.inverse(inertia), world);
            }
        }
    }
}
