using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Ai
{
    /// <summary>Closed-form arrival, heading and one-tick ability commands for an exclusive AI root (SPEC-23 §5.5).</summary>
    [BurstCompile, WithNone(typeof(PlayerShip), typeof(Hulk))]
    public partial struct AiSteerJob : IJobEntity
    {
        [ReadOnly] public ComponentLookup<FlightState> Flights;
        [ReadOnly] public ComponentLookup<PropulsionStats> Propulsion;
        [ReadOnly] public ComponentLookup<ShipTarget> Targets;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<VoxelGrid> Grids;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        public AiConfig Config;
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
            if (distance >= Config.ArrivalRadius)
            {
                float braking = Propulsion.HasComponent(entity) ? math.max(.5f, .5f * Propulsion[entity].ForwardAccel) : 2;
                float speed = math.min(brain.GoalSpeedFraction * flight.ForwardCap,
                    math.sqrt(2 * braking * math.max(0, distance - Config.ArrivalRadius)));
                desired = math.normalizesafe(to) * speed;
            }
            float2 local = FlightMath.ToLocalPlanar(desired, pose.Rotation);
            cmd.Thrust.x = math.clamp(local.x / math.max(1, flight.StrafeCap), -1, 1);
            cmd.Thrust.y = local.y >= 0 ? math.clamp(local.y / math.max(1, flight.ForwardCap), 0, 1)
                : math.clamp(local.y / math.max(1, flight.ReverseCap), -1, 0);
            if (math.abs(cmd.Thrust.y) < .002f && math.abs(flight.LocalVelocity.y) > 1)
                cmd.Thrust.y = -.002f * math.sign(flight.LocalVelocity.y);
            command = cmd;
        }

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
