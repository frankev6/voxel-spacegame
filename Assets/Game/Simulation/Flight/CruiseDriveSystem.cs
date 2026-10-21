using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Voxel.Damage;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Flight
{
    /// <summary>ADR-0122 §6: a cruising ship flies along its nose at <see cref="Cruise.Speed"/>, turning at a fraction of its yaw cap and
    /// never strafing. Runs after propulsion (which skips cruising ships) and before the physics step, on the same body, so collisions,
    /// the floating origin and the camera are unchanged. On the tick cruise ends it brings the ship down to flight speed.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateAfter(typeof(PropulsionSystem))]
    public partial struct CruiseDriveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<FlightConfig>();
            state.RequireForUpdate<CruiseConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new DriveJob { Flight = SystemAPI.GetSingleton<FlightConfig>(), Config = SystemAPI.GetSingleton<CruiseConfig>() }
                .ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithNone(typeof(Hulk))]
        partial struct DriveJob : IJobEntity
        {
            public FlightConfig Flight;
            public CruiseConfig Config;

            void Execute(in Cruise cruise, in ShipCommand command, in FlightState flight, in LocalTransform pose, in PhysicsMass mass,
                ref PhysicsVelocity velocity)
            {
                if (mass.InverseMass <= 0) return;
                if (cruise.Phase == CruisePhase.On)
                {
                    float3 nose = math.normalizesafe(new float3(math.forward(pose.Rotation).x, 0, math.forward(pose.Rotation).z));
                    velocity.Linear = nose * cruise.Speed;
                    float yawCap = flight.YawRateCap > 0 ? flight.YawRateCap : math.radians(Flight.MaxYawRateDeg);
                    float yaw = math.clamp(command.Turn, -1, 1) * yawCap * Config.TurnFraction;
                    quaternion inertia = math.mul(pose.Rotation, mass.InertiaOrientation);
                    velocity.Angular = math.rotate(math.inverse(inertia), new float3(0, yaw, 0));
                }
                else if (cruise.Exit != 0)
                {
                    float cap = flight.ForwardCap > 0 ? flight.ForwardCap : Flight.MaxForwardSpeed;
                    float speed = math.length(velocity.Linear.xz);
                    if (speed > cap) velocity.Linear *= cap / speed;
                }
            }
        }
    }
}
