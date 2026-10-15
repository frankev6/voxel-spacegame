using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Systems;
using Game.Data.Components;
using Game.Data.Definitions;
using Game.Simulation.Commands;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace Game.Galaxy.Interaction
{
    /// <summary>SPEC-31 §5.11 edge consumption, per pilot (ADR-0117 decider, authority only): each Interact press with an in-range target
    /// publishes BodyInteracted for that pilot. A co-op guest's presses reach here through its commands, so its salvage is decided by
    /// the host exactly like the host player's. Other pilots may only salvage for now: docking, jumping, hailing and inspecting are
    /// single-player flows (scene changes, dialogue) that the co-op spike does not run for guests.</summary>
    [BurstCompile, UpdateInGroup(typeof(WorldGroup)), UpdateAfter(typeof(InteractionQuerySystem))]
    public partial struct InteractionTriggerSystem : ISystem
    {
        EntityQuery untracked;
        TriggerJob job;
        [BurstCompile] public void OnCreate(ref SystemState state)
        {
            untracked = new EntityQueryBuilder(Allocator.Temp).WithAll<Pilot>().WithNone<PilotInteractSeen>().Build(ref state);
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<ManagedEventQueue<BodyInteracted>>();
            _ = state.GetComponentTypeHandle<ManagedEventQueue<BodyInteracted>>();
            job.Locals = state.GetComponentLookup<PlayerShip>(true); job.Ids = state.GetComponentLookup<PersistentId>(true);
        }
        public void OnUpdate(ref SystemState state)
        {
            if (!untracked.IsEmptyIgnoreFilter) state.EntityManager.AddComponent<PilotInteractSeen>(untracked);
            job.Locals.Update(ref state); job.Ids.Update(ref state);
            job.Tick = SystemAPI.TryGetSingleton<SimClock>(out var clock) ? clock.Tick : 0;
            job.Events = SystemAPI.GetSingleton<ManagedEventQueue<BodyInteracted>>().Queue.AsParallelWriter();
            state.Dependency = job.Schedule(state.Dependency);
        }

        [BurstCompile]
        partial struct TriggerJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<PlayerShip> Locals;
            [ReadOnly] public ComponentLookup<PersistentId> Ids;
            public NativeQueue<ManagedEventEnvelope<BodyInteracted>>.ParallelWriter Events;
            public uint Tick;
            void Execute(Entity pilot, in PilotInteractCount count, in PilotInteraction interaction, ref PilotInteractSeen seen)
            {
                // A pilot's presses from before it was tracked are not replayed.
                if (seen.Initialized == 0) { seen.Value = count.Value; seen.Initialized = 1; return; }
                var target = interaction.Value;
                while (seen.Value != count.Value)
                {
                    seen.Value = unchecked((byte)(seen.Value + 1));
                    if (target.InRange == 0) continue;
                    if (!Locals.HasComponent(pilot) && target.Kind != InteractionKind.Salvage) continue;
                    ulong pilotId = Ids.HasComponent(pilot) ? Ids[pilot].Value : 0;
                    Events.Enqueue(new ManagedEventEnvelope<BodyInteracted> { Tick = Tick, SortKey = target.Body.LocalKey,
                        Payload = new BodyInteracted(target.Body.System.Value, target.Body.LocalKey, (byte)target.Kind, target.GridPersistentId, pilotId) });
                }
            }
        }
    }
}
