using Game.Core.Components;
using Game.Core.Events;
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
    /// <summary>SPEC-31 §5.11: real collider surface distance and variable-rate edge consumption.</summary>
    [BurstCompile]
    public struct InteractionQueryJob : IJob
    {
        static readonly ProfilerMarker QueryMarker = new ProfilerMarker("InteractionQuerySystem.Query");
        static readonly ProfilerMarker EdgeMarker = new ProfilerMarker("InteractionQuerySystem.Edge");
        public Entity Player, Singleton;
        public byte Counter;
        public uint Tick;
        public float QueryRadius;
        public float SalvageBonusM;   // QA-001: the player's salvage magnets extend Salvage targets' reach
        [ReadOnly] public CollisionWorld Physics;
        [ReadOnly] public SpatialHashQueryData Spatial;
        [ReadOnly] public ComponentLookup<LocalTransform> Transforms;
        [ReadOnly] public ComponentLookup<Interactable> Interactables;
        [ReadOnly] public ComponentLookup<BodyGridOwner> Owners;
        [ReadOnly] public ComponentLookup<StarSystemBody> Bodies;
        [ReadOnly] public ComponentLookup<PersistentId> Ids;
        public ComponentLookup<InteractionTarget> Targets;
        public ComponentLookup<InteractionInputState> Inputs;
        public NativeQueue<ManagedEventEnvelope<BodyInteracted>>.ParallelWriter Events;
        public void Execute()
        {
            var target = default(InteractionTarget);
            using (QueryMarker.Auto())
            {
                if (Player != Entity.Null && Transforms.HasComponent(Player))
                {
                    float3 center = Transforms[Player].Position; float radius = 10;
                    int proxy = Spatial.FindIndex(Player);
                    if (proxy >= 0) { var value = Spatial.Get(proxy); center = value.Center; radius = value.HullRadius; }
                    using var hits = new NativeList<DistanceHit>(Allocator.Temp);
                    var hitStorage = hits;
                    var collector = new AllHitsCollector<DistanceHit>(QueryRadius + radius, ref hitStorage);
                    Physics.CalculateDistance(new PointDistanceInput { Position = center, MaxDistance = QueryRadius + radius,
                        Filter = PhysicsLayers.InteractionQuery }, ref collector);
                    for (int i = 0; i < hits.Length; i++)
                    {
                        var hit = hits[i]; Entity candidate = hit.Entity;
                        if (candidate == Player) continue;
                        if (!Interactables.HasComponent(candidate) && Owners.HasComponent(candidate)) candidate = Owners[candidate].Anchor;
                        if (!Interactables.HasComponent(candidate)) continue;
                        if (Bodies.HasComponent(candidate) && Bodies[candidate].Grid == Player) continue;
                        var interactable = Interactables[candidate];
                        float distance = math.max(0, hit.Distance - radius);
                        float reach = interactable.DistanceM + (interactable.Kind == InteractionKind.Salvage ? SalvageBonusM : 0);
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
                }
            }
            Targets[Singleton] = target;
            using (EdgeMarker.Auto())
            {
                var input = Inputs[Singleton]; input.Initialized = 1;
                // Consuming even without a player/target prevents a stale press firing on arrival.
                while (input.LastInteractCounter != Counter)
                {
                    input.LastInteractCounter = unchecked((byte)(input.LastInteractCounter + 1));
                    if (target.InRange == 0) continue;
                    Events.Enqueue(new ManagedEventEnvelope<BodyInteracted> { Tick = Tick, SortKey = target.Body.LocalKey,
                        Payload = new BodyInteracted(target.Body.System.Value, target.Body.LocalKey, (byte)target.Kind, target.GridPersistentId) });
                }
                Inputs[Singleton] = input;
            }
        }
    }
}
