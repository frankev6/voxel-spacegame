using Game.Core.Components;
using Game.Core.Systems;
using Game.Voxel.Damage;
using Game.Voxel.PhysicsBody;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Game.Galaxy.Bodies
{
    internal struct BodyGridEventQueue : IComponentData
    {
        public NativeQueue<BodyGridEvent> Events;
    }

    /// <summary>Reports core loss and asteroid depletion; the loader owns persistent body-state writes.</summary>
    [BurstCompile, UpdateInGroup(typeof(WorldGroup))]
    [UpdateAfter(typeof(Game.Galaxy.Interaction.InteractionQuerySystem))]
    public partial struct BodyGridWatchSystem : ISystem
    {
        NativeQueue<BodyGridEvent> events;
        ComponentLookup<StarSystemBody> anchors;
        ComponentLookup<Hulk> hulks;
        ComponentLookup<BodyGridInitialCells> initial;
        ComponentLookup<GridMass> masses;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            events = new NativeQueue<BodyGridEvent>(Allocator.Persistent);
            state.EntityManager.CreateSingleton(new BodyGridEventQueue { Events = events });
            anchors = state.GetComponentLookup<StarSystemBody>(true);
            hulks = state.GetComponentLookup<Hulk>(true);
            initial = state.GetComponentLookup<BodyGridInitialCells>(true);
            masses = state.GetComponentLookup<GridMass>(true);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<WorldSceneActive>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            anchors.Update(ref state); hulks.Update(ref state); initial.Update(ref state); masses.Update(ref state);
            state.Dependency = new WatchJob { Events = events, Anchors = anchors, Hulks = hulks,
                Initial = initial, Masses = masses }.Schedule(state.Dependency);
        }
        [BurstCompile] public void OnDestroy(ref SystemState state)
        { state.Dependency.Complete(); if (events.IsCreated) events.Dispose(); }
        [BurstCompile]
        partial struct WatchJob : IJobEntity
        {
            public NativeQueue<BodyGridEvent> Events;
            [ReadOnly] public ComponentLookup<StarSystemBody> Anchors;
            [ReadOnly] public ComponentLookup<Hulk> Hulks;
            [ReadOnly] public ComponentLookup<BodyGridInitialCells> Initial;
            [ReadOnly] public ComponentLookup<GridMass> Masses;
            void Execute(Entity root, in BodyGridOwner owner)
            {
                if (!Anchors.TryGetComponent(owner.Anchor, out var anchor) || (anchor.Flags & BodyFlags.Destroyed) != 0) return;
                if (Hulks.HasComponent(root)) Events.Enqueue(new BodyGridEvent { Body = owner.Body, Grid = root, Kind = 0 });
                else if ((anchor.Flags & BodyFlags.Asteroid) != 0 && Initial.TryGetComponent(root, out var initial) &&
                    Masses.TryGetComponent(root, out var mass) && mass.Data.CellCount < initial.Cells * .1)
                    Events.Enqueue(new BodyGridEvent { Body = owner.Body, Grid = root, Kind = 1 });
            }
        }
    }
}
