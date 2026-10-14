using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Galaxy.Bodies;
using Game.Simulation.Commands;
using Game.Simulation.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Transforms;

namespace Game.Galaxy.Interaction
{
    /// <summary>Owns boot-lifetime interaction state and schedules one query after the frame's streaming work.</summary>
    [BurstCompile, UpdateInGroup(typeof(WorldGroup))]
    public partial struct InteractionQuerySystem : ISystem
    {
        Entity singleton;
        EntityQuery players, active;
        InteractionQueryJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            singleton = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<InteractionTarget>(singleton);
            state.EntityManager.AddComponent<InteractionInputState>(singleton);
            players = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip, LocalTransform>().WithNone<Parent>().Build(ref state);
            active = new EntityQueryBuilder(Allocator.Temp).WithAll<WorldSceneActive>().Build(ref state);
            job = new InteractionQueryJob { Singleton = singleton,
                Transforms = state.GetComponentLookup<LocalTransform>(true), Interactables = state.GetComponentLookup<Interactable>(true),
                Owners = state.GetComponentLookup<BodyGridOwner>(true), Bodies = state.GetComponentLookup<StarSystemBody>(true),
                Ids = state.GetComponentLookup<PersistentId>(true), Targets = state.GetComponentLookup<InteractionTarget>(),
                Inputs = state.GetComponentLookup<InteractionInputState>() };
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<PlayerCommand>(); state.RequireForUpdate<StreamingConfig>();
            state.RequireForUpdate<PhysicsWorldSingleton>(); state.RequireForUpdate<SpatialHash>();
            state.RequireForUpdate<ManagedEventQueue<BodyInteracted>>();
            _ = state.GetComponentTypeHandle<ManagedEventQueue<BodyInteracted>>();
        }
        [BurstCompile] public void OnUpdate(ref SystemState state)
        {
            job.Transforms.Update(ref state); job.Interactables.Update(ref state); job.Owners.Update(ref state);
            job.Bodies.Update(ref state); job.Ids.Update(ref state); job.Targets.Update(ref state); job.Inputs.Update(ref state);
            job.Player = active.IsEmptyIgnoreFilter || players.IsEmptyIgnoreFilter ? Entity.Null : players.GetSingletonEntity();
            job.Counter = SystemAPI.GetSingleton<PlayerCommand>().Edges.Interact;
            job.Tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0;
            job.QueryRadius = SystemAPI.GetSingleton<StreamingConfig>().InteractionQueryRadiusM;
            job.Physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var spatial = SystemAPI.GetSingleton<SpatialHash>(); job.Spatial = spatial.AsReadOnly();
            job.Events = SystemAPI.GetSingleton<ManagedEventQueue<BodyInteracted>>().Queue.AsParallelWriter();
            state.Dependency = job.Schedule(spatial.BeginRead(state.Dependency));
            spatial.EndRead(state.Dependency);
        }
    }
}
