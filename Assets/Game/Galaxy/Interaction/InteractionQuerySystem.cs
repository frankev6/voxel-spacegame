using Game.Core.Components;
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
    /// <summary>Owns boot-lifetime interaction state and schedules one query after the frame's streaming work: every pilot's nearest
    /// interactable (ADR-0117 derivation, every machine). InteractionTriggerSystem (authority) acts on the pilots' presses.</summary>
    [BurstCompile, UpdateInGroup(typeof(WorldGroup))]
    public partial struct InteractionQuerySystem : ISystem
    {
        Entity singleton;
        EntityQuery pilots, local, active, untracked;
        InteractionQueryJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            singleton = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<InteractionTarget>(singleton);
            pilots = new EntityQueryBuilder(Allocator.Temp).WithAll<Pilot, LocalTransform, PilotInteraction>().WithNone<Parent>().Build(ref state);
            local = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerShip, LocalTransform>().WithNone<Parent>().Build(ref state);
            untracked = new EntityQueryBuilder(Allocator.Temp).WithAll<Pilot>().WithNone<PilotInteraction>().Build(ref state);
            active = new EntityQueryBuilder(Allocator.Temp).WithAll<WorldSceneActive>().Build(ref state);
            job = new InteractionQueryJob { Singleton = singleton,
                Transforms = state.GetComponentLookup<LocalTransform>(true), Interactables = state.GetComponentLookup<Interactable>(true),
                Owners = state.GetComponentLookup<Game.Galaxy.Bodies.BodyGridOwner>(true), Bodies = state.GetComponentLookup<Game.Galaxy.Bodies.StarSystemBody>(true),
                Ids = state.GetComponentLookup<PersistentId>(true), Reaches = state.GetComponentLookup<Salvage.SalvageReach>(true),
                Targets = state.GetComponentLookup<InteractionTarget>(), PilotTargets = state.GetComponentLookup<PilotInteraction>() };
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<StreamingConfig>();
            state.RequireForUpdate<PhysicsWorldSingleton>(); state.RequireForUpdate<SpatialHash>();
        }
        public void OnUpdate(ref SystemState state)
        {
            if (!untracked.IsEmptyIgnoreFilter) state.EntityManager.AddComponent<PilotInteraction>(untracked);
            job.Transforms.Update(ref state); job.Interactables.Update(ref state); job.Owners.Update(ref state); job.Bodies.Update(ref state);
            job.Ids.Update(ref state); job.Reaches.Update(ref state); job.Targets.Update(ref state); job.PilotTargets.Update(ref state);
            bool inWorld = !active.IsEmptyIgnoreFilter;
            job.Local = inWorld && local.CalculateEntityCount() == 1 ? local.GetSingletonEntity() : Entity.Null;
            job.Pilots = inWorld ? pilots.ToEntityArray(state.WorldUpdateAllocator) : new NativeArray<Entity>(0, state.WorldUpdateAllocator);
            job.QueryRadius = SystemAPI.GetSingleton<StreamingConfig>().InteractionQueryRadiusM;
            job.Physics = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
            var spatial = SystemAPI.GetSingleton<SpatialHash>(); job.Spatial = spatial.AsReadOnly();
            state.Dependency = job.Schedule(spatial.BeginRead(state.Dependency));
            spatial.EndRead(state.Dependency);
        }
    }
}
