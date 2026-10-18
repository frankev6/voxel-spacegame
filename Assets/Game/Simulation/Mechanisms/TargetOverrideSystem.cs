using Game.Core.Components;
using Game.Core.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Mechanisms
{
    /// <summary>SPEC-91 HAZ-020 (ADR-0119 decision 6), on the authority: the owner of TargetOverride. Applies last tick's TargetPolicy
    /// mechanism actions to the roots of the action's group (Value 0 clears the override, 1 follows the race leader), then points every
    /// leader override at RaceLapSignal.Leader. The AI think (AiThinkJob) gives a valid override precedence over its own choice.</summary>
    [UpdateInGroup(typeof(ShipCommandGroup)), UpdateBefore(typeof(Game.Simulation.Ai.AiDecisionSystem))]
    public partial struct TargetOverrideSystem : ISystem
    {
        public const byte None = 0, Leader = 1, Fixed = 2;
        uint appliedActions;
        EntityQuery overrides, grouped;

        public void OnCreate(ref SystemState state)
        {
            appliedActions = uint.MaxValue;
            overrides = state.GetEntityQuery(typeof(TargetOverride));
            grouped = state.GetEntityQuery(ComponentType.ReadOnly<MechanismGroup>(), ComponentType.ReadOnly<Game.Simulation.Ai.AiBrain>());
            state.RequireForUpdate<SimClock>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (SystemAPI.TryGetSingleton<MechanismActions>(out var actions) && actions.Current.IsCreated && actions.Current.Length > 0 && actions.Tick != appliedActions)
            {
                appliedActions = actions.Tick;
                bool any = false;
                foreach (var a in actions.Current) any |= a.Kind == MechanismActionKind.TargetPolicy;
                if (any)
                {
                    state.CompleteDependency();
                    using var roots = grouped.ToEntityArray(Allocator.Temp);
                    foreach (var a in actions.Current)
                    {
                        if (a.Kind != MechanismActionKind.TargetPolicy) continue;
                        byte policy = (byte)math.clamp((int)math.round(a.Value), None, Leader);
                        foreach (var r in roots)
                        {
                            if (em.GetComponentData<MechanismGroup>(r).Group != a.Group) continue;
                            var value = new TargetOverride { Policy = policy };
                            if (em.HasComponent<TargetOverride>(r)) em.SetComponentData(r, value); else em.AddComponentData(r, value);
                        }
                    }
                }
            }
            if (overrides.IsEmpty) return;
            Entity leader = SystemAPI.TryGetSingleton<RaceLapSignal>(out var signal) ? signal.Leader : Entity.Null;
            state.CompleteDependency();
            using var entities = overrides.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                var o = em.GetComponentData<TargetOverride>(e);
                if (o.Policy == Leader && o.Target != leader) { o.Target = leader; em.SetComponentData(e, o); }
                else if (o.Policy == None && o.Target != Entity.Null) { o.Target = Entity.Null; em.SetComponentData(e, o); }
            }
        }
    }
}
