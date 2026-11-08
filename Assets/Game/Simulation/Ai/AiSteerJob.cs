using Game.Core;
using Game.Core.Components;
using Game.Data.Combat;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Power;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Simulation.Ai
{
    /// <summary>Arrival, heading and one-tick ability commands for an exclusive AI root (SPEC-23 §5.5). A ship with propulsion stats is
    /// flown by the shared pilot (PilotControl): its own way when it heads where it goes (drifting through turns), holding the brain's
    /// heading otherwise (on a target, broadside, in formation). Without stats the closed form of §5.5 steps 2–4. In experimental 3D
    /// flight (ADR-0123, amended 2026-11-08) the ship stays level and Lift takes it to its target's height while engaging, else to
    /// its goal's.</summary>
    [BurstCompile, WithNone(typeof(PlayerShip), typeof(Hulk))]
    public partial struct AiSteerJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<FlightState> Flights;
        [ReadOnly] public ComponentLookup<FleetMember> Members;
        [ReadOnly] public ComponentLookup<PropulsionStats> Propulsion;
        [ReadOnly] public ComponentLookup<ShipTarget> Targets;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<PhysicsVelocity> Velocities;
        [ReadOnly] public ComponentLookup<ShipStatMultipliers> Multipliers;
        [ReadOnly] public ComponentLookup<ShieldState> Shields;
        [ReadOnly] public BufferLookup<FlightThruster> Thrusters;
        [ReadOnly] public ComponentLookup<Flight3D> Flight3D;   // ADR-0123
        public AiConfig Config;
        public FlightConfig FlightConfig;
        public uint Tick;

        void Execute(Entity entity, in AiBrain brain, ref ShipCommand command, in LocalTransform pose)
        {
            var cmd = default(ShipCommand);
            // A pending spawn must not retain a pre-conversion command or consume old decisions.
            if (Spawning.HasComponent(entity) && Spawning.IsComponentEnabled(entity)) { command = cmd; return; }
            // Brain bits start at 0; command abilities start at bit 1 (Interact must never leak through).
            if (brain.AbilityEdgesTick == Tick) cmd.Edges = (ShipCommandEdges)((brain.AbilityEdges & 0x0f) << 1);
            SetAim(entity, ref cmd);
            ref var profiles = ref Config.Profiles.Value.Profiles;
            ref var profile = ref profiles[brain.ProfileIndex < profiles.Length ? brain.ProfileIndex : 0];
            // FlightState is optional in the query so stations and incomplete flight roots still get fresh commands.
            if (profile.Role == AiRole.Turret || !Flights.HasComponent(entity)) { command = cmd; return; }
            var flight = Flights[entity];
            float yaw = Planar.YawFromForward(math.rotate(pose.Rotation, new float3(0, 0, 1)));
            float error = Planar.WrapAngle(brain.GoalHeadingYaw - yaw);
            cmd.Turn = flight.YawRateCap > 0 ? math.clamp(error / (flight.YawRateCap * .5f), -1, 1) : 0;
            float3 to = Planar.FlattenY(brain.GoalPosition - pose.Position);
            float distance = math.length(to);
            float3 desired = float3.zero;
            float arrival = Members.HasComponent(entity) && Members[entity].Leader != Entity.Null
                ? math.min(Config.ArrivalRadius, 5f) : Config.ArrivalRadius;
            float approach = 0;   // m/s it may still close at and stop on the goal
            if (distance >= arrival)
            {
                float braking = Propulsion.HasComponent(entity) ? math.max(.5f, .5f * Propulsion[entity].ForwardAccel) : 2;
                approach = math.sqrt(2 * braking * math.max(0, distance - arrival));
                desired = math.normalizesafe(to) * math.min(brain.GoalSpeedFraction * flight.ForwardCap, approach);
            }
            if (Propulsion.HasComponent(entity) && Velocities.HasComponent(entity))
            {
                var multipliers = Multipliers.HasComponent(entity) ? Multipliers[entity] : default;
                float speedMultiplier = FlightMath.SpeedMultiplier(FlightConfig, multipliers.Get(StatId.MaxSpeed), Shields.HasComponent(entity) && Shields[entity].Active != 0);
                var envelope = PilotEnvelope.From(Propulsion[entity], FlightConfig, speedMultiplier, FlightMath.YawRateCap(FlightConfig, multipliers.Get(StatId.TurnRate)),
                    Thrusters.HasBuffer(entity) ? PilotEnvelope.ForwardSpool(Thrusters[entity]) : 0);
                float2 velocity = Velocities[entity].Linear.xz, want = math.normalizesafe(to.xz) * math.min(brain.GoalSpeedFraction * envelope.Cruise, approach);   // on this tick's caps
                // Heading where it goes (flee, return, patrol): the nose is the pilot's, so it drifts through what strafe alone cannot.
                bool travel = distance >= arrival && math.abs(Planar.WrapAngle(brain.GoalHeadingYaw - math.atan2(to.x, to.z))) < TravelHeading;
                ShipCommand flown;
                if (travel)
                {
                    float wantYaw = math.lengthsq(want) > .25f ? math.atan2(want.x, want.y) : yaw;
                    float nose = math.clamp(Planar.WrapAngle(yaw - wantYaw), -PilotControl.MaxNose, PilotControl.MaxNose);   // its nose now carries the angle
                    flown = PilotControl.Fly(envelope, flight, pose.Rotation, velocity, want, 0, envelope.Cruise, ref nose);
                }
                else flown = PilotControl.Face(envelope, flight, pose.Rotation, velocity, want, brain.GoalHeadingYaw, envelope.Cruise);
                cmd.Turn = flown.Turn; cmd.Thrust = flown.Thrust;
            }
            else
            {
                float2 local = FlightMath.ToLocalPlanar(desired, pose.Rotation);
                cmd.Thrust.x = math.clamp(local.x / math.max(1, flight.StrafeCap), -1, 1);
                cmd.Thrust.y = local.y >= 0 ? math.clamp(local.y / math.max(1, flight.ForwardCap), 0, 1)
                    : math.clamp(local.y / math.max(1, flight.ReverseCap), -1, 0);
            }
            if (math.abs(cmd.Thrust.y) < .002f && math.abs(flight.LocalVelocity.y) > 1)
                cmd.Thrust.y = -.002f * math.sign(flight.LocalVelocity.y);
            if (Flight3D.HasComponent(entity) && Flight3D.IsComponentEnabled(entity))
            {
                // The brain's goals are on the plane; while engaging, the height to hold is the target's.
                float goalHeight = brain.State == AiState.Engage && cmd.AimValid != 0 ? cmd.AimWorld.y : brain.GoalPosition.y;
                var multipliers = Multipliers.HasComponent(entity) ? Multipliers[entity] : default;
                float speedMultiplier = FlightMath.SpeedMultiplier(FlightConfig, multipliers.Get(StatId.MaxSpeed), Shields.HasComponent(entity) && Shields[entity].Active != 0);
                cmd.Lift = Lift(FlightConfig, speedMultiplier, pose.Position.y, goalHeight);
            }
            command = cmd;
        }

        /// <summary>m: within this of the height to hold, Lift is 0 and the vertical assist holds the ship there.</summary>
        public const float HeightTolerance = .5f;

        /// <summary>ADR-0123 (amended 2026-11-08): the Lift that closes on <paramref name="goalHeight"/> at the vertical speed cap and
        /// brakes in time, at half the vertical assist's acceleration so a weak or shielded ship still stops on it.</summary>
        public static float Lift(in FlightConfig config, float speedMultiplier, float height, float goalHeight)
        {
            float cap = config.MaxForwardSpeed * speedMultiplier * config.VerticalSpeedFraction;
            float error = goalHeight - height;
            if (cap <= 0 || !math.isfinite(error)) return 0;
            float speed = math.sqrt(config.VerticalAccel * math.max(0, math.abs(error) - HeightTolerance));   // √(2 · accel/2 · d)
            return math.clamp(math.sign(error) * math.min(cap, speed) / cap, -1, 1);
        }

        /// <summary>rad: a goal heading this close to the way to the goal counts as heading where it goes.</summary>
        public const float TravelHeading = .2f;

        void SetAim(Entity entity, ref ShipCommand command)
        {
            Entity target = Targets.HasComponent(entity) ? Targets[entity].Value : Entity.Null;
            if (!Transforms.HasComponent(target) || Hulks.HasComponent(target) ||
                Spawning.HasComponent(target) && Spawning.IsComponentEnabled(target)) return;
            var targetPose = Transforms[target]; float3 center = targetPose.Position;
            if (Grids.HasComponent(target))
            {
                var grid = Grids[target];
                // Same occupied-bounds centre as SpatialProxy and WeaponAimJob; weapons apply their own lead.
                center = targetPose.TransformPoint(((float3)grid.CellMin + (float3)(grid.CellMax + 1)) * (.5f * grid.CellSize));
            }
            command.AimWorld = center; command.AimValid = 1;
        }
    }
}
