using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Commands;
using Game.Simulation.Weapons;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Ai
{
    /// <summary>SPEC-23 §5.7: formation goals and deterministic leader replacement; never writes physics.</summary>
    [BurstCompile, UpdateInGroup(typeof(ShipCommandGroup))]
    [UpdateAfter(typeof(AiDecisionSystem)), UpdateBefore(typeof(AiSteeringSystem))]
    public partial struct FleetSystem : ISystem
    {
        EntityQuery members;
        FleetUpdateJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<AiConfig>();
            members = new EntityQueryBuilder(Allocator.Temp).WithAll<FleetMember, AiBrain, ShipTarget, LocalTransform>()
                .WithNone<PlayerShip, Hulk>().Build(ref state);
            job.Members = state.GetComponentLookup<FleetMember>();
            job.Brains = state.GetComponentLookup<AiBrain>();
            job.Targets = state.GetComponentLookup<ShipTarget>();
            job.Poses = state.GetComponentLookup<LocalTransform>(true);
            job.Ids = state.GetComponentLookup<PersistentId>(true);
            job.Hulks = state.GetComponentLookup<Hulk>(true);
            job.Spawning = state.GetComponentLookup<GridSpawning>(true);
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Members.Update(ref state); job.Brains.Update(ref state); job.Targets.Update(ref state);
            job.Poses.Update(ref state); job.Ids.Update(ref state); job.Hulks.Update(ref state); job.Spawning.Update(ref state);
            job.Config = SystemAPI.GetSingleton<AiConfig>();
            var roots = members.ToEntityListAsync(Allocator.TempJob, state.Dependency, out var gathered);
            job.Roots = roots;
            var handle = job.Schedule(gathered);
            state.Dependency = roots.Dispose(handle);
        }
    }

    [BurstCompile] internal struct FleetUpdateJob : IJob
    {
        [ReadOnly] public NativeList<Entity> Roots;
        public ComponentLookup<FleetMember> Members;
        public ComponentLookup<AiBrain> Brains;
        public ComponentLookup<ShipTarget> Targets;
        [ReadOnly] public ComponentLookup<LocalTransform> Poses;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        [ReadOnly] public ComponentLookup<Hulk> Hulks;
        [ReadOnly] public ComponentLookup<GridSpawning> Spawning;
        public AiConfig Config;
        bool Live(Entity root) => root != Entity.Null && Poses.HasComponent(root) && Brains.HasComponent(root)
            && Targets.HasComponent(root) && !Hulks.HasComponent(root)
            && !(Spawning.HasComponent(root) && Spawning.IsComponentEnabled(root));
        public void Execute()
        {
            // One serial job owns all member writes. Snapshot keys avoid promotion depending on iteration order.
            var original = new NativeArray<FleetMember>(Roots.Length, Allocator.Temp);
            var replacements = new NativeHashMap<Entity, Entity>(math.max(1, Roots.Length), Allocator.Temp);
            for (int i = 0; i < Roots.Length; i++) original[i] = Members[Roots[i]];
            for (int i = 0; i < Roots.Length; i++)
            {
                var root = Roots[i]; var member = original[i];
                if (!Live(root) || member.Leader == Entity.Null || Live(member.Leader)) continue;
                if (!replacements.TryGetValue(member.Leader, out var selected)) replacements.Add(member.Leader, root);
                else
                {
                    var chosen = Members[selected];
                    ulong id = Ids.HasComponent(root) ? Ids[root].Value : (ulong)root.Index;
                    ulong chosenId = Ids.HasComponent(selected) ? Ids[selected].Value : (ulong)selected.Index;
                    if (member.Slot < chosen.Slot || member.Slot == chosen.Slot &&
                        (id < chosenId || id == chosenId && root.Index < selected.Index)) replacements[member.Leader] = root;
                }
            }
            for (int i = 0; i < Roots.Length; i++)
            {
                var member = original[i]; var root = Roots[i];
                if (replacements.TryGetValue(member.Leader, out var promoted))
                { member.Leader = root == promoted ? Entity.Null : promoted; Members[root] = member; }
            }
            for (int i = 0; i < Roots.Length; i++)
            {
                var root = Roots[i]; var member = Members[root];
                if (!Live(root) || !Live(member.Leader) || member.Leader == root) continue;
                ref var formations = ref Config.Profiles.Value.Formations;
                if (member.FormationIndex >= formations.Length) continue;
                ref var slots = ref formations[member.FormationIndex].Slots;
                if (member.Slot >= slots.Length) continue;
                var pose = Poses[member.Leader]; var leaderBrain = Brains[member.Leader]; var brain = Brains[root];
                float yaw = Planar.YawFromForward(math.rotate(pose.Rotation, new float3(0, 0, 1)));
                float2 slot = slots[member.Slot];
                brain.GoalPosition = pose.Position + math.rotate(quaternion.RotateY(yaw), new float3(slot.x, 0, slot.y));
                brain.GoalHeadingYaw = yaw; brain.GoalSpeedFraction = 1;
                brain.State = leaderBrain.State;
                Brains[root] = brain; Targets[root] = Targets[member.Leader];
            }
            replacements.Dispose(); original.Dispose();
        }
    }
}
