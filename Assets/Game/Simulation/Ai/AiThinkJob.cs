using System.Threading;
using Game.Core;
using Game.Core.Components;
using Game.Data.Definitions;
using Game.Simulation.Abilities;
using Game.Simulation.Commands;
using Game.Simulation.Power;
using Game.Simulation.Spatial;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.Rendering;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Ai
{
    /// <summary>Target, state, goal, ability and fire decisions for one exclusive root (SPEC-23 §5.4).</summary>
    [BurstCompile]
    [WithNone(typeof(PlayerShip), typeof(Hulk))]
    public partial struct AiThinkJob : IJobEntity
    {
        [ReadOnly] public SpatialHashQueryData Hash;
        [ReadOnly] public FactionRelations Relations;
        [ReadOnly] public EntityStorageInfoLookup Entities;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        [ReadOnly] public ComponentLookup<Dormant> Dormant;
        [ReadOnly] public ComponentLookup<ShipStats> ShipStats;
        [ReadOnly] public ComponentLookup<WeaponStats> WeaponStats;
        [ReadOnly] public ComponentLookup<ShieldState> Shields;
        [ReadOnly] public ComponentLookup<ShieldControl> ShieldControls;
        [ReadOnly] public BufferLookup<AbilitySlot> Abilities;
        [ReadOnly] public ComponentLookup<FleetMember> Members;
        [ReadOnly] internal NativeHashMap<Entity, FleetLeaderDecision> Leaders;
        public byte MemberPass;
        // Only atomic telemetry writes cross roots. Neither counter nor warning order affects decisions.
        [NativeDisableParallelForRestriction] public NativeArray<int> Overflow;
        public AiConfig Config;
        public uint Tick;

        void Execute(Entity entity, ref AiBrain brain, ref ShipTarget target, ref WeaponControl control,
            in Allegiance allegiance, in LocalTransform pose, in PersistentId persistentId)
        {
            if (Spawning.HasComponent(entity) && Spawning.IsComponentEnabled(entity)) return;
            Entity leader = Members.HasComponent(entity) ? Members[entity].Leader : Entity.Null;
            bool following = leader != Entity.Null && leader != entity && Entities.Exists(leader)
                && !Hulks.HasComponent(leader) && !(Spawning.HasComponent(leader) && Spawning.IsComponentEnabled(leader));
            if (following != (MemberPass != 0)) return;
            ref var profiles = ref Config.Profiles.Value.Profiles;
            ref var profile = ref profiles[brain.ProfileIndex < profiles.Length ? brain.ProfileIndex : 0];
            int interval = profile.ThinkIntervalTicks;
            if (Dormant.HasComponent(entity) && Dormant.IsComponentEnabled(entity)) interval *= Config.DormantThinkMultiplier;
            if (unchecked(Tick + (uint)persistentId.Value) % (uint)interval != 0) return;
            brain.ThinkCount++;
            brain.LastThinkTick = Tick;
            var rng = Random.CreateFromIndex((uint)StableHash.Combine(
                StableHash.Combine(Tick, persistentId.Value), (ulong)RngPurpose.AiDecision));
            brain.PreviousState = brain.State;
            Entity previousTarget = target.Value;
            int current = Hash.FindIndex(target.Value);
            if (current < 0 || !Hostile(Hash.Get(current), entity, allegiance)) current = -1;
            int ownIndex = Hash.FindIndex(entity);
            float3 center = ownIndex >= 0 ? Hash.Get(ownIndex).Center : pose.Position;
            float ownRadius = ownIndex >= 0 ? Hash.Get(ownIndex).HullRadius : 0;
            float hull = ShipStats.HasComponent(entity) ? ShipStats[entity].MassFraction : 1;
            float weaponRange = WeaponStats.HasComponent(entity) ? WeaponStats[entity].MaxWeaponRange : profile.SensorRange * .5f;
            AiRole role = profile.Role == AiRole.Combat && weaponRange == 0 ? AiRole.Civilian : profile.Role;
            if (following && Leaders.TryGetValue(leader, out var leaderDecision))
            {
                target = leaderDecision.Target;
                if (target.Value != Entity.Null && (!Entities.Exists(target.Value) || Hulks.HasComponent(target.Value))) target.Value = Entity.Null;
                brain.TargetSinceTick = target.SinceTick;
                brain.State = leaderDecision.State;
                SetAbilities(ref brain, ref profile, entity, hull, target.Value != Entity.Null);
                int targetIndex = Hash.FindIndex(target.Value);
                brain.FireBlocked = (byte)(profile.AvoidFriendlyFire != 0 && targetIndex >= 0
                    && FriendlyFire(entity, target.Value, allegiance, pose.Position, Hash.Get(targetIndex).Center) ? 1 : 0);
                control = new WeaponControl { Autofire = (byte)(brain.State == AiState.Engage
                    && targetIndex >= 0 && brain.FireBlocked == 0 ? 1 : 0), AimMode = WeaponAimMode.TargetLead };
                return;
            }
            CountSphereQuery();
            var candidates = new FixedList512Bytes<int>();
            CheckOverflow(Hash.QuerySphere(center, profile.SensorRange, ref candidates), candidates.Length);
            int nearest = -1, selected = -1;
            float nearestDistance = float.PositiveInfinity, bestScore = float.PositiveInfinity;
            for (int i = 0; i < candidates.Length; i++)
            {
                int index = candidates[i];
                var proxy = Hash.Get(index);
                if (!Hostile(proxy, entity, allegiance)) continue;
                float distance = math.distance(center, proxy.Center);
                // The hash includes intersecting hulls; sensor membership is centre distance (§5.4).
                if (distance > profile.SensorRange) continue;
                if (distance < nearestDistance) { nearestDistance = distance; nearest = index; }
                float score = distance / math.max(1, weaponRange)
                    - ((proxy.Flags & ProxyFlags.PlayerShip) != 0 ? profile.PlayerBias : 0)
                    - (index == current ? profile.Stickiness : 0);
                if (role != AiRole.Civilian && score < bestScore) { selected = index; bestScore = score; }
            }
            float anchorDistance = math.distance(Planar.FlattenY(pose.Position), Planar.FlattenY(brain.Anchor));
            bool returning = brain.PreviousState == AiState.Return && anchorDistance >= Config.ArrivalRadius * 2;
            if (role == AiRole.Turret) brain.State = AiState.Hold;
            else if (nearest >= 0 && (role == AiRole.Civilian || profile.FleeHullFraction > 0 && hull < profile.FleeHullFraction))
                brain.State = AiState.Flee;
            else if (returning || profile.LeashRadius > 0 && anchorDistance > profile.LeashRadius * (selected >= 0 ? 1.5f : 1))
            { brain.State = AiState.Return; selected = -1; }
            else if (selected >= 0) brain.State = AiState.Engage;
            else brain.State = profile.PatrolRadius > 0 ? AiState.Patrol : AiState.Idle;
            target.Value = selected >= 0 ? Hash.Get(selected).Entity : Entity.Null;
            if (target.Value != previousTarget) target.SinceTick = Tick;
            brain.TargetSinceTick = target.SinceTick;
            SetGoal(ref brain, ref profile, pose, selected, nearest, weaponRange, ref rng);
            if (brain.State != AiState.Idle && brain.State != AiState.Hold)
                Avoid(ref brain, entity, target.Value, pose.Position, ownRadius);
            SetAbilities(ref brain, ref profile, entity, hull, target.Value != Entity.Null);
            brain.FireBlocked = (byte)(profile.AvoidFriendlyFire != 0 && selected >= 0
                && FriendlyFire(entity, target.Value, allegiance, pose.Position, Hash.Get(selected).Center) ? 1 : 0);
            control = new WeaponControl
            {
                Autofire = (byte)((brain.State == AiState.Engage || brain.State == AiState.Hold)
                    && selected >= 0 && brain.FireBlocked == 0 ? 1 : 0),
                AimMode = WeaponAimMode.TargetLead,
            };
        }

        bool Hostile(in SpatialProxy proxy, Entity self, in Allegiance allegiance) =>
            proxy.Entity != self && Entities.Exists(proxy.Entity) && !Hulks.HasComponent(proxy.Entity)
            && (proxy.Kind == ProxyKind.Ship || proxy.Kind == ProxyKind.Station)
            && Relations.IsHostile(allegiance, proxy.Allegiance);

        void SetGoal(ref AiBrain brain, ref AiProfile profile, in LocalTransform pose, int target, int threat,
            float weaponRange, ref Random rng)
        {
            float3 position = Planar.FlattenY(pose.Position);
            float3 heading = math.rotate(pose.Rotation, new float3(0, 0, 1));
            switch (brain.State)
            {
                case AiState.Engage:
                    float3 targetCenter = Planar.FlattenY(Hash.Get(target).Center);
                    float3 toSelf = math.normalizesafe(position - targetCenter, new float3(1, 0, 0));
                    if (profile.OrbitFlipThinks > 0 && brain.ThinkCount % profile.OrbitFlipThinks == 0 && rng.NextFloat() < .5f)
                        brain.OrbitSign = (sbyte)-brain.OrbitSign;
                    brain.GoalPosition = targetCenter + math.rotate(quaternion.RotateY(brain.OrbitSign * profile.OrbitStepRad), toSelf)
                        * math.max(profile.MinEngageRange, weaponRange * profile.PreferredRangeFraction);
                    heading = -toSelf;
                    if (profile.Facing == AiFacing.Broadside)
                        heading = math.rotate(quaternion.RotateY(brain.OrbitSign * math.PI * .5f), heading);
                    brain.GoalSpeedFraction = profile.Throttle;
                    break;
                case AiState.Flee:
                    brain.GoalPosition = position + math.normalizesafe(position - Planar.FlattenY(Hash.Get(threat).Center), new float3(1, 0, 0)) * 1500;
                    heading = brain.GoalPosition - position;
                    brain.GoalSpeedFraction = 1;
                    break;
                case AiState.Return:
                    brain.GoalPosition = Planar.FlattenY(brain.Anchor);
                    heading = brain.GoalPosition - position;
                    brain.GoalSpeedFraction = profile.Throttle;
                    break;
                case AiState.Patrol:
                    if (brain.ThinkCount == 1 || math.distance(position, Planar.FlattenY(brain.GoalPosition)) < Config.ArrivalRadius)
                    {
                        float patrolDistance = rng.NextFloat() * profile.PatrolRadius;
                        float patrolAngle = rng.NextFloat(0, 2 * math.PI);
                        brain.GoalPosition = Planar.FlattenY(brain.Anchor) + math.rotate(quaternion.RotateY(patrolAngle),
                            new float3(patrolDistance, 0, 0));
                    }
                    heading = brain.GoalPosition - position;
                    brain.GoalSpeedFraction = .5f * profile.Throttle;
                    break;
                default:
                    brain.GoalPosition = position;
                    brain.GoalSpeedFraction = 0;
                    break;
            }
            brain.GoalHeadingYaw = Planar.YawFromForward(heading);
        }

        void Avoid(ref AiBrain brain, Entity self, Entity target, float3 position, float radius)
        {
            position = Planar.FlattenY(position);
            var candidates = new FixedList512Bytes<int>();
            CheckOverflow(Hash.QuerySegment(position, brain.GoalPosition, radius + Config.AvoidanceMargin, ref candidates), candidates.Length);
            float3 direction = brain.GoalPosition - position;
            float3 side = math.normalizesafe(new float3(direction.z, 0, -direction.x), new float3(1, 0, 0));
            for (int i = 0; i < candidates.Length; i++)
            {
                var proxy = Hash.Get(candidates[i]);
                if (proxy.Entity == self || proxy.Entity == target || !Entities.Exists(proxy.Entity)) continue;
                float clearance = proxy.HullRadius + radius + Config.AvoidanceMargin;
                if (!Crosses(position, brain.GoalPosition, proxy.Center, clearance)) continue;
                brain.GoalPosition += side * (math.dot(proxy.Center - position, side) > 0 ? -clearance : clearance);
                break;
            }
        }

        bool FriendlyFire(Entity self, Entity target, in Allegiance allegiance, float3 position, float3 destination)
        {
            var candidates = new FixedList512Bytes<int>();
            CheckOverflow(Hash.QuerySegment(position, destination, 2, ref candidates), candidates.Length);
            float3 direction = destination - position;
            float lengthSq = math.lengthsq(direction);
            for (int i = 0; i < candidates.Length; i++)
            {
                var proxy = Hash.Get(candidates[i]);
                if (proxy.Entity == self || proxy.Entity == target || !Entities.Exists(proxy.Entity)
                    || Relations.IsHostile(allegiance, proxy.Allegiance)) continue;
                float along = math.dot(proxy.Center - position, direction);
                if (along < 0 || along >= lengthSq) continue;
                if (Crosses(position, destination, proxy.Center, proxy.HullRadius)) return true;
            }
            return false;
        }

        static bool Crosses(float3 start, float3 end, float3 center, float radius)
        {
            float3 delta = end - start;
            float lengthSq = math.lengthsq(delta);
            float t = lengthSq > 0 ? math.saturate(math.dot(center - start, delta) / lengthSq) : 0;
            return math.distancesq(center, start + delta * t) <= radius * radius;
        }

        void SetAbilities(ref AiBrain brain, ref AiProfile profile, Entity entity, float hull, bool hasTarget)
        {
            brain.AbilityEdges = 0;
            brain.AbilityEdgesTick = Tick;
            if (!Abilities.HasBuffer(entity)) return;
            var slots = Abilities[entity];
            var shield = Shields.HasComponent(entity) ? Shields[entity] : default;
            for (int i = 0; i < profile.AbilityRules.Length; i++)
            {
                var rule = profile.AbilityRules[i];
                bool trigger = rule.Trigger switch
                {
                    AiAbilityTrigger.EngageStart => brain.State == AiState.Engage && brain.PreviousState != AiState.Engage,
                    AiAbilityTrigger.Fleeing => brain.State == AiState.Flee,
                    AiAbilityTrigger.HullBelow => hull < rule.Threshold,
                    AiAbilityTrigger.ShieldBelow => shield.MaxHp > 0 && shield.Hp < rule.Threshold * shield.MaxHp,
                    AiAbilityTrigger.ShieldOff => shield.MaxHp > 0 && ShieldControls.HasComponent(entity) && ShieldControls[entity].RequestedOn == 0,
                    AiAbilityTrigger.HasTarget => hasTarget,
                    _ => false,
                };
                if (!trigger) continue;
                for (int k = 0; k < math.min(4, slots.Length); k++)
                    if (slots[k].AbilityIndex == rule.AbilityIndex && slots[k].State == AbilitySlotState.Ready && slots[k].Blocked == 0)
                    { brain.AbilityEdges |= (byte)(1 << k); break; }
            }
        }

        unsafe void CountSphereQuery()
        {
            int* counters = (int*)Overflow.GetUnsafePtr();
            Interlocked.Increment(ref counters[2]);
        }

        unsafe void CheckOverflow(int total, int retained)
        {
            if (total <= retained) return;
            int* counters = (int*)Overflow.GetUnsafePtr();
            Interlocked.Increment(ref counters[1]);
            if (Interlocked.CompareExchange(ref counters[0], 1, 0) != 0) return;
            FixedString128Bytes message = "AI spatial query truncated to its lowest 127 proxy indices; later overflows counted.";
            GameLog.Burst.Warn(message);
        }
    }
}
