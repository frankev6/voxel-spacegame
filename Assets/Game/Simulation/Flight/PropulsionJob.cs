using Game.Core;
using Game.Data.Blocks;
using Game.Simulation.Commands;
using Game.Simulation.Common;
using Game.Voxel.Damage;
using Game.Voxel.Model;
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
    /// <summary>Allocates thruster forces and applies impulses, without replacing motion. Implements SPEC-20 §5.3.</summary>
    [BurstCompile]
    [WithNone(typeof(Hulk))]
    public partial struct PropulsionJob : IJobEntity
    {
        public FlightConfig Config;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<BlockModulesBlob> Modules;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
        [ReadOnly] public EntityStorageInfoLookup Entities;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        // Each thruster entity belongs to exactly one ship root and is written only by that root's execute (SPEC-20 §5.3).
        [NativeDisableParallelForRestriction] public ComponentLookup<ThrusterState> Thrusters;

        void Execute(Entity entity, in ShipCommand command, ref FlightState flight, ref DynamicBuffer<FlightThruster> thrusters,
            in VoxelGrid grid, in DynamicBuffer<BlockInstance> instances, in LocalTransform transform,
            ref PhysicsVelocity velocity, in PhysicsMass mass)
        {
            if (mass.InverseMass <= 0 || (Spawning.HasComponent(entity) && Spawning.IsComponentEnabled(entity))) return;
            if ((flight.Flags & FlightFlags.Initialized) == 0 || flight.SourceRevision != grid.Revision)
                Rebuild(ref flight, thrusters, instances, grid);

            const float dt = SimTime.FixedTimestep;
            float bodyMass = 1f / mass.InverseMass;
            float inertia = mass.InverseInertia.y > 0 ? 1f / mass.InverseInertia.y : 0;
            quaternion rotation = transform.Rotation;
            quaternion inertiaRotation = math.mul(rotation, mass.InertiaOrientation);
            float2 local = FlightMath.ToLocalPlanar(velocity.Linear, rotation);
            float yaw = math.rotate(inertiaRotation, velocity.Angular).y;
            // TODO(FLT-012): read power and stat multipliers; M1 uses powered thrusters and unit multipliers.
            bool boost = (command.Flags & ShipCommandFlags.Boost) != 0 && (command.Flags & ShipCommandFlags.Brake) == 0;
            FlightMath.SpeedCaps(Config, 1, boost, out float forward, out float reverse, out float strafe);
            float yawCap = FlightMath.YawRateCap(Config, 1);
            FlightMath.Targets(Config, command.Thrust, command.Turn, command.Flags, local, forward, reverse, strafe,
                yawCap, out float2 target, out float targetYaw);
            var problem = new ThrustProblem
            {
                Target = new float3(bodyMass * (target - local) * Config.LinearAssistGain / dt,
                    inertia * (targetYaw - yaw) * Config.AngularAssistGain / dt),
                Weights = new float3(1f / math.max(1, bodyMass * 10), 1f / math.max(1, bodyMass * 10),
                    Config.TorqueWeight / math.max(1, inertia)), Sweeps = Config.AllocationSweeps
            };
            var actuators = new NativeArray<ThrustActuator>(thrusters.Length + 1, Allocator.Temp);
            var throttles = new NativeArray<float>(actuators.Length, Allocator.Temp);
            for (int k = 0; k < thrusters.Length; k++)
            {
                var thruster = thrusters[k];
                float max = Entities.Exists(thruster.Thruster) ? (boost ? Config.BoostThrottle : 1) : 0;
                actuators[k] = new ThrustActuator
                {
                    Column = thruster.MaxForce * new float3(thruster.ForceDirLocal.x, thruster.ForceDirLocal.z,
                        math.cross(thruster.PositionLocal - mass.CenterOfMass, thruster.ForceDirLocal).y), Max = max
                };
                throttles[k] = math.clamp(thruster.Throttle, 0, max);
            }
            float gyroTorque = FlightMath.GyroYawTorque(Config, mass.InverseInertia.y);
            int gyroIndex = thrusters.Length;
            actuators[gyroIndex] = new ThrustActuator { Column = new float3(0, 0, gyroTorque), Min = -1, Max = 1 };
            throttles[gyroIndex] = flight.Gyro;
            // Bound impossible requests before comparing axes: a target speed is not an unlimited engine.
            // Each bound is an outer bound only; the allocator still resolves coupled force/torque.
            float3 minimum = 0, maximum = 0;
            for (int k = 0; k < actuators.Length; k++)
            {
                var actuator = actuators[k];
                minimum += math.min(actuator.Column * actuator.Min, actuator.Column * actuator.Max);
                maximum += math.max(actuator.Column * actuator.Min, actuator.Column * actuator.Max);
            }
            problem.Target = math.clamp(problem.Target, minimum, maximum);
            ThrustAllocator.Solve(problem, actuators, throttles);
            bool firing = false;
            for (int k = 0; k < thrusters.Length; k++)
            {
                var thruster = thrusters[k];
                float throttle = throttles[k] < Config.ThrottleEpsilon ? 0 : throttles[k];
                if (throttle > 0)
                {
                    float3 force = math.rotate(rotation, thruster.ForceDirLocal) * (thruster.MaxForce * throttle);
                    velocity.ApplyImpulse(mass, transform.Position, rotation, force * dt, transform.TransformPoint(thruster.PositionLocal));
                    firing = true;
                }
                thruster.Throttle = throttle;
                thrusters[k] = thruster;
                if (Thrusters.HasComponent(thruster.Thruster))
                {
                    float intensity = Thrusters[thruster.Thruster].Intensity;
                    float desired = math.saturate(throttle);
                    intensity += (desired - intensity) * (desired > intensity ? Config.IntensityRise : Config.IntensityFall);
                    Thrusters[thruster.Thruster] = new ThrusterState { Intensity = intensity < .005f ? 0 : intensity };
                }
            }
            float gyro = math.abs(throttles[gyroIndex]) < Config.ThrottleEpsilon ? 0 : throttles[gyroIndex];
            velocity.ApplyAngularImpulse(mass, math.rotate(math.inverse(inertiaRotation), new float3(0, gyro * gyroTorque * dt, 0)));
            flight.Gyro = gyro; flight.GyroTorque = gyroTorque;
            flight.LocalVelocity = local; flight.YawRate = yaw;
            flight.ForwardCap = forward; flight.ReverseCap = reverse; flight.StrafeCap = strafe; flight.YawRateCap = yawCap;
            flight.TargetLocalVelocity = target; flight.TargetYawRate = targetYaw;
            flight.Flags = firing ? flight.Flags | FlightFlags.Firing : flight.Flags & ~FlightFlags.Firing;
            throttles.Dispose(); actuators.Dispose();
        }

        void Rebuild(ref FlightState flight, DynamicBuffer<FlightThruster> thrusters, in DynamicBuffer<BlockInstance> instances, in VoxelGrid grid)
        {
            var rebuilt = new NativeList<FlightThruster>(Allocator.Temp);
            int oldIndex = 0;
            for (int i = 0; i < instances.Length; i++)
            {
                var instance = instances[i];
                if (instance.Block == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0 ||
                    instance.Functional == Entity.Null || !Entities.Exists(instance.Functional)) continue;
                ref var definition = ref Blocks.Value.Blocks[instance.Block];
                if (!definition.HasModule(BlockModuleId.Thruster)) continue;
                ref var orientations = ref Orientations.Value;
                float3 direction = GridSpaceMath.BlockDirToGrid(new float3(0, 0, -1), ref orientations, instance.Orient);
                direction.y = 0;
                if (math.lengthsq(direction) < .0001f || !Modules.Value.TryGet(BlockModuleId.Thruster, instance.Block, out ThrusterParams row)) continue;
                while (oldIndex < thrusters.Length && thrusters[oldIndex].InstanceIndex < i) oldIndex++;
                float throttle = oldIndex < thrusters.Length && thrusters[oldIndex].InstanceIndex == i ? thrusters[oldIndex].Throttle : 0;
                rebuilt.Add(new FlightThruster
                {
                    Thruster = instance.Functional, InstanceIndex = i, ForceDirLocal = math.normalize(direction),
                    PositionLocal = GridSpaceMath.InstanceCenter(ref definition, ref orientations, instance.Orient, instance.Origin, grid.CellSize),
                    MaxForce = row.ThrustNewtons, Throttle = throttle
                });
            }
            thrusters.CopyFrom(rebuilt.AsArray());
            rebuilt.Dispose();
            flight.SourceRevision = grid.Revision;
            flight.Flags |= FlightFlags.Initialized;
        }
    }
}
