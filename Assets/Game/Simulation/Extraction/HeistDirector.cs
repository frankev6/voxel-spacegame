using Game.Core;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Simulation.Mechanisms;
using Game.Voxel.Damage;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Game.Simulation.Extraction
{
    /// <summary>SPEC-93 EXT-006, authority (ADR-0117 decider): each heist place's state machine. Calm until its core is freed or a crew
    /// ship is Wanted by its faction; then the alarm runs its AlarmStep timeline once, step by step, from the alarm tick (Lockdown when
    /// the last step has fired). Over when the core is socketed (Installed), when the core is past EscapeRadius (Escaped), or when no
    /// crew ship is left alive during the alarm (Caught).</summary>
    [UpdateInGroup(typeof(GameplayPostPhysicsGroup)), UpdateAfter(typeof(WatchSystem)), UpdateBefore(typeof(MechanismSystem))]
    public partial struct HeistDirector : ISystem
    {
        EntityQuery places, crew;

        public void OnCreate(ref SystemState state)
        {
            places = SystemAPI.QueryBuilder().WithAllRW<HeistState>().WithAllRW<AlarmStep>().Build();
            crew = SystemAPI.QueryBuilder().WithAll<Transponder, LocalTransform>().Build();
            if (!SystemAPI.HasSingleton<HeistSignals>())
            {
                var e = state.EntityManager.CreateEntity(typeof(HeistSignals));
                state.EntityManager.SetName(e, "HeistSignals");
                state.EntityManager.SetComponentData(e, new HeistSignals { Pending = new NativeList<HeistSignal>(8, Allocator.Persistent) });
            }
            state.RequireForUpdate<SimClock>(); state.RequireForUpdate(places);
        }

        public void OnDestroy(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonRW<HeistSignals>(out var s) && s.ValueRO.Pending.IsCreated) s.ValueRW.Pending.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var signals = SystemAPI.GetSingleton<HeistSignals>().Pending;
            bool canRequest = SystemAPI.TryGetSingleton<MechanismRequests>(out var requests) && requests.Pending.IsCreated;
            using var all = places.ToEntityArray(Allocator.Temp);
            using var ships = crew.ToEntityArray(Allocator.Temp);
            foreach (var place in all)
            {
                var h = em.GetComponentData<HeistState>(place);
                if (h.Phase == HeistPhase.Over) continue;
                bool hasCore = h.Core != Entity.Null && em.Exists(h.Core) && em.HasComponent<StationCore>(h.Core);
                var core = hasCore ? em.GetComponentData<StationCore>(h.Core) : default;

                if (h.Phase == HeistPhase.Calm)
                {
                    var cause = AlarmCause.None;
                    if (hasCore && core.Freed != 0) cause = AlarmCause.CoreFreed;
                    else foreach (var s in ships) if (TransponderApi.IsWantedBy(em, s, h.Faction)) { cause = AlarmCause.Identified; break; }
                    if (cause != AlarmCause.None) Start(ref h, place, cause, tick, signals);
                }

                if (h.Phase == HeistPhase.Alarm || h.Phase == HeistPhase.Lockdown)
                {
                    float elapsed = (tick - h.AlarmTick) * SimTime.FixedTimestep;
                    var steps = em.GetBuffer<AlarmStep>(place);
                    bool left = false;
                    for (int i = 0; i < steps.Length; i++)
                    {
                        var step = steps[i];
                        if (step.Done != 0) continue;
                        if (step.AtSeconds > elapsed) { left = true; continue; }
                        step.Done = 1; steps[i] = step;
                        if (step.Kind == AlarmStepKind.Mechanism) { if (canRequest) requests.Pending.Add(new MechanismAction { Kind = step.Action, Group = step.Group, Value = step.Value }); }
                        else signals.Add(new HeistSignal { Place = place, Kind = step.Kind, Group = step.Group, Value = step.Value, Index = (byte)i, Phase = h.Phase });
                        if (step.Kind == AlarmStepKind.Cruiser) h.CruiserArrived = 1;
                    }
                    if (!left && h.Phase == HeistPhase.Alarm) Phase(ref h, place, HeistPhase.Lockdown, signals);
                }

                // outcomes
                var outcome = HeistOutcome.None;
                if (hasCore && core.Socket != Entity.Null) outcome = HeistOutcome.Installed;
                else if (hasCore && core.Freed != 0 && h.EscapeRadius > 0 && Far(em, h.Core, core, h.EscapeRadius)) outcome = HeistOutcome.Escaped;
                else if (h.Phase != HeistPhase.Calm && !AnyAlive(em, ships)) outcome = HeistOutcome.Caught;
                if (outcome != HeistOutcome.None)
                {
                    h.Outcome = outcome; h.OverTick = tick;
                    Phase(ref h, place, HeistPhase.Over, signals);
                }
                em.SetComponentData(place, h);
            }
        }

        static void Start(ref HeistState h, Entity place, AlarmCause cause, uint tick, NativeList<HeistSignal> signals)
        {
            h.Cause = cause; h.AlarmTick = tick;
            Phase(ref h, place, HeistPhase.Alarm, signals);
        }

        static void Phase(ref HeistState h, Entity place, HeistPhase phase, NativeList<HeistSignal> signals)
        {
            h.Phase = phase;
            signals.Add(new HeistSignal { Place = place, Kind = AlarmStepKind.Message, Index = 255, Phase = phase, Outcome = h.Outcome });
        }

        static bool Far(EntityManager em, Entity coreEntity, in StationCore core, float radius)
        {
            if (core.Holder == Entity.Null || !em.Exists(core.Holder) || !em.HasComponent<LocalTransform>(core.Holder)) return false;
            float3 home = em.GetComponentData<LocalTransform>(core.Holder).TransformPoint(core.HoldLocal);
            if (em.HasComponent<Stowed>(coreEntity))
            {   // a stowed core rides in its ship
                var ship = em.GetComponentData<Stowed>(coreEntity).Ship;
                return em.Exists(ship) && math.distance(em.GetComponentData<LocalTransform>(ship).Position, home) > radius;
            }
            return math.distance(em.GetComponentData<LocalTransform>(coreEntity).Position, home) > radius;
        }

        static bool AnyAlive(EntityManager em, NativeArray<Entity> ships)
        {
            foreach (var s in ships) if (em.Exists(s) && !em.HasComponent<Hulk>(s)) return true;
            return false;
        }
    }
}
