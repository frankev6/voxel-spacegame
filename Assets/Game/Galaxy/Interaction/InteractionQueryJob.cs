using Game.Core.Components;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Galaxy.Bodies;
using Game.Simulation.Spatial;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Profiling;
using Unity.Transforms;

namespace Game.Galaxy.Interaction
{
    /// <summary>SPEC-31 §5.11: real collider surface distance, per pilot (ADR-0117). Each pilot's nearest interactable goes to its
    /// <see cref="PilotInteraction"/>, and the local pilot's also to the <see cref="InteractionTarget"/> singleton. Acting on presses is
    /// InteractionTriggerJob's (authority).</summary>
    [BurstCompile]
    public struct InteractionQueryJob : IJob
    {
        static readonly ProfilerMarker QueryMarker = new ProfilerMarker("InteractionQuerySystem.Query");
        [ReadOnly] public NativeArray<Entity> Pilots;
        public Entity Local, Singleton;
        public float QueryRadius;
        [ReadOnly] public CollisionWorld Physics;
        [ReadOnly] public SpatialHashQueryData Spatial;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<Interactable> Interactables;
        [ReadOnly] public ComponentLookup<BodyGridOwner> Owners;
        [ReadOnly] public ComponentLookup<StarSystemBody> Bodies;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        [ReadOnly] public ComponentLookup<Salvage.SalvageReach> Reaches;   // QA-001: a pilot's salvage magnets extend Salvage targets' reach
        public ComponentLookup<InteractionTarget> Targets;
        public ComponentLookup<PilotInteraction> PilotTargets;
        public void Execute()
        {
            using var marker = QueryMarker.Auto();
            var local = default(InteractionTarget);
            foreach (var pilot in Pilots)
            {
                var target = Query(pilot);
                if (PilotTargets.HasComponent(pilot)) PilotTargets[pilot] = new PilotInteraction { Value = target };
                if (pilot == Local) local = target;
            }
            Targets[Singleton] = local;
        }

        InteractionTarget Query(Entity player)
        {
            var target = default(InteractionTarget);
            if (player == Entity.Null || !Transforms.HasComponent(player)) return target;
            float salvageBonus = Reaches.HasComponent(player) ? Reaches[player].BonusM : 0;
            float3 center = Transforms[player].Position; float radius = 10;
            int proxy = Spatial.FindIndex(player);
            if (proxy >= 0) { var value = Spatial.Get(proxy); center = value.Center; radius = value.HullRadius; }
            using var hits = new NativeList<DistanceHit>(Allocator.Temp);
            var hitStorage = hits;
            var collector = new AllHitsCollector<DistanceHit>(QueryRadius + radius, ref hitStorage);
            Physics.CalculateDistance(new PointDistanceInput { Position = center, MaxDistance = QueryRadius + radius,
                Filter = PhysicsLayers.InteractionQuery }, ref collector);
            for (int i = 0; i < hits.Length; i++)
            {
                var hit = hits[i]; Entity candidate = hit.Entity;
                if (candidate == player) continue;
                if (!Interactables.HasComponent(candidate) && Owners.HasComponent(candidate)) candidate = Owners[candidate].Anchor;
                if (!Interactables.HasComponent(candidate)) continue;
                if (Bodies.HasComponent(candidate) && Bodies[candidate].Grid == player) continue;
                var interactable = Interactables[candidate];
                float distance = math.max(0, hit.Distance - radius);
                float reach = interactable.DistanceM + (interactable.Kind == InteractionKind.Salvage ? salvageBonus : 0);
                if (distance > 2 * reach) continue;
                byte inRange = (byte)(distance <= reach ? 1 : 0);
                if (target.Target != Entity.Null && (inRange < target.InRange ||
                    inRange == target.InRange && (distance > target.DistanceM ||
                    distance == target.DistanceM && candidate.Index >= target.Target.Index))) continue;
                Entity grid = Bodies.HasComponent(candidate) ? Bodies[candidate].Grid : hit.Entity;
                ulong id = Ids.HasComponent(grid) ? Ids[grid].Value : 0;
                target = new InteractionTarget { Target = candidate, Body = interactable.Body, Kind = interactable.Kind,
                    InRange = inRange, TemplateIndex = interactable.TemplateIndex, DistanceM = distance, GridPersistentId = id };
            }
            return target;
        }
    }
}
