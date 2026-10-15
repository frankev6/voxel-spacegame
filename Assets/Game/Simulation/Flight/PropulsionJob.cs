using Game.Core;
using Game.Data.Combat;
using Game.Simulation.Power;
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
        internal const float WarmStartDecay = .9f;
        public FlightConfig Config;
        [ReadOnly] public BlobAssetReference<BlockDefsBlob> Blocks;
        [ReadOnly] public BlobAssetReference<BlockModulesBlob> Modules;
        [ReadOnly] public BlobAssetReference<OrientationTablesBlob> Orientations;
        [ReadOnly] public EntityStorageInfoLookup Entities;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<Powered> Powered;
        [ReadOnly] public ComponentLookup<ShipStatMultipliers> Multipliers;
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
            var multipliers = Multipliers.HasComponent(entity) ? Multipliers[entity] : default;
            float thrustMultiplier = multipliers.Get(StatId.Thrust);
            float turnMultiplier = multipliers.Get(StatId.TurnRate);
            bool boost = (command.Flags & ShipCommandFlags.Boost) != 0 && (command.Flags & ShipCommandFlags.Brake) == 0;
            FlightMath.SpeedCaps(Config, multipliers.Get(StatId.MaxSpeed), boost, out float forward, out float reverse, out float strafe);
            float yawCap = FlightMath.YawRateCap(Config, turnMultiplier);
            FlightMath.Targets(Config, command.Thrust, command.Turn, command.Flags, local, forward, reverse, strafe,
                yawCap, out float2 target, out float targetYaw);
            var problem = new ThrustProblem
            {
                Target = new float3(bodyMass * (target - local) * Config.LinearAssistGain / dt,
                    inertia * (targetYaw - yaw) * Config.AngularAssistGain / dt),
                Weights = new float3(1f / math.max(1, bodyMass * 10), 1f / math.max(1, bodyMass * 10),
                    Config.TorqueWeight / math.max(1, inertia)), Sweeps = Config.AllocationSweeps
            };
            var actuators = new NativeArray<ThrustActuator>(thrusters.Length + 3, Allocator.Temp);
            var throttles = new NativeArray<float>(actuators.Length, Allocator.Temp);
            for (int k = 0; k < thrusters.Length; k++)
            {
                var thruster = thrusters[k];
                bool usable = Entities.Exists(thruster.Thruster) &&
                    (!Powered.HasComponent(thruster.Thruster) || Powered.IsComponentEnabled(thruster.Thruster));
                float max = usable ? (boost ? Config.BoostThrottle : 1) : 0;
                actuators[k] = new ThrustActuator
                {
                    Column = thruster.MaxForce * thrustMultiplier * new float3(thruster.ForceDirLocal.x, thruster.ForceDirLocal.z,
                        math.cross(thruster.PositionLocal - mass.CenterOfMass, thruster.ForceDirLocal).y), Max = max
                };
                throttles[k] = math.clamp(thruster.Throttle, 0, max);
            }
            float gyroTorque = FlightMath.GyroYawTorque(Config, mass.InverseInertia.y) * turnMultiplier;
            int gyroIndex = thrusters.Length;
            actuators[gyroIndex] = new ThrustActuator { Column = new float3(0, 0, gyroTorque), Min = -1, Max = 1 };
            throttles[gyroIndex] = flight.Gyro;
            // ADR-0114: the core's manoeuvring assist acts at the centre of mass along local X and Z, like the gyro for yaw.
            float coreForce = FlightMath.CoreAssistForce(Config, mass.InverseMass) * thrustMultiplier;
            actuators[gyroIndex + 1] = new ThrustActuator { Column = new float3(coreForce, 0, 0), Min = -1, Max = 1 };
            // Boost overdrives the forward push like the thrusters (QA-001: boost must be felt), never braking or strafing.
            actuators[gyroIndex + 2] = new ThrustActuator { Column = new float3(0, coreForce, 0), Min = -1, Max = boost ? Config.BoostThrottle : 1 };
            throttles[gyroIndex + 1] = flight.CoreAssist.x; throttles[gyroIndex + 2] = flight.CoreAssist.y;
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
            // QA-001 (owner: "when the ship is stationary, why are the thrusters on?"): least squares has a null space once
            // actuators overlap (side thrusters against the core assist, engines against assist braking, balanced by the
            // gyro). Gauss–Seidel never leaves it and the warm start carried it forever, so a resting cruiser kept 12
            // thrusters at full burn. Shrinking the warm start each tick bleeds that out (0.9^60 ≈ 0.2% after a second);
            // the solve itself is unchanged and restores any throttle the target still needs within its sweeps.
            for (int k = 0; k < throttles.Length; k++) throttles[k] *= WarmStartDecay;
            ThrustAllocator.Solve(problem, actuators, throttles);
            bool firing = false;
            for (int k = 0; k < thrusters.Length; k++)
            {
                var thruster = thrusters[k];
                float throttle = throttles[k] < Config.ThrottleEpsilon ? 0 : throttles[k];
                if (throttle > 0)
                {
                    float3 force = math.rotate(rotation, thruster.ForceDirLocal) * (thruster.MaxForce * thrustMultiplier * throttle);
                    velocity.ApplyImpulse(mass, transform.Position, rotation, force * dt, transform.TransformPoint(thruster.PositionLocal));
                    firing = true;
                }
                thruster.Throttle = throttle;
                thrusters[k] = thruster;
                if (Thrusters.HasComponent(thruster.Thruster))
                {
                    float intensity = Thrusters[thruster.Thruster].Intensity;
                    float desired = math.clamp(throttle, 0, Config.BoostThrottle); // boost overdrive shows as a longer plume (QA-001)
                    intensity += (desired - intensity) * (desired > intensity ? Config.IntensityRise : Config.IntensityFall);
                    Thrusters[thruster.Thruster] = new ThrusterState { Intensity = intensity < .005f ? 0 : intensity };
                }
            }
            float gyro = math.abs(throttles[gyroIndex]) < Config.ThrottleEpsilon ? 0 : throttles[gyroIndex];
            velocity.ApplyAngularImpulse(mass, math.rotate(math.inverse(inertiaRotation), new float3(0, gyro * gyroTorque * dt, 0)));
            flight.Gyro = gyro; flight.GyroTorque = gyroTorque;
            float2 core = new float2(throttles[gyroIndex + 1], throttles[gyroIndex + 2]);
            core = math.select(core, 0, math.abs(core) < Config.ThrottleEpsilon);
            if (coreForce > 0 && math.any(core != 0))
                velocity.ApplyLinearImpulse(mass, math.rotate(rotation, new float3(core.x, 0, core.y)) * (coreForce * dt));
            flight.CoreAssist = core; flight.CoreAssistForce = coreForce;
            flight.Flags = firing ? flight.Flags | FlightFlags.Firing : flight.Flags & ~FlightFlags.Firing;
            flight.LocalVelocity = local; flight.YawRate = yaw;
            flight.ForwardCap = forward; flight.ReverseCap = reverse; flight.StrafeCap = strafe; flight.YawRateCap = yawCap;
            flight.TargetLocalVelocity = target; flight.TargetYawRate = targetYaw;
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
