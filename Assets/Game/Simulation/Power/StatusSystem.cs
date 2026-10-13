using System.Collections.Generic;
using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Combat;
using Game.Data.Streams;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Simulation.Power.InhibitRequest>))]
[assembly: RegisterGenericJobType(typeof(SimEventGather.Job<Game.Simulation.Power.StatModifierRequest>))]

namespace Game.Simulation.Power
{
    /// <summary>SPEC-22 §5.3b / ADR-0016: sole writer of inhibit deadlines and active stat modifiers.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplayPrePhysicsGroup))]
    [UpdateAfter(typeof(PowerDemandGroup))]
    [UpdateBefore(typeof(PowerAllocationSystem))]
    public partial struct StatusSystem : ISystem, ISystemStartStop
    {
        StatusChannels owned;
        Entity channelEntity;
        NativeList<InhibitRequest> inhibits;
        NativeList<StatModifierRequest> modifiers;
        NativeList<OrderedInhibit> sortedInhibits;
        NativeList<OrderedModifier> sortedModifiers;
        ComponentLookup<Inhibited> inhibited;
        BufferLookup<BlockInstance> instances;
        BufferLookup<ShipStatModifier> modifierBuffers;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            owned = new StatusChannels
            {
                StatModifiers = SimEventChannel<StatModifierRequest>.Create("stat_modifiers"),
                Inhibits = SimEventChannel<InhibitRequest>.Create("inhibits")
            };
            channelEntity = state.EntityManager.CreateSingleton(owned);
            inhibits = new NativeList<InhibitRequest>(16, Allocator.Persistent);
            modifiers = new NativeList<StatModifierRequest>(16, Allocator.Persistent);
            sortedInhibits = new NativeList<OrderedInhibit>(16, Allocator.Persistent);
            sortedModifiers = new NativeList<OrderedModifier>(16, Allocator.Persistent);
            inhibited = state.GetComponentLookup<Inhibited>();
            instances = state.GetBufferLookup<BlockInstance>(true);
            modifierBuffers = state.GetBufferLookup<ShipStatModifier>();
            state.RequireForUpdate<GameplayActive>();
            state.RequireForUpdate<SimClock>();
        }

        [BurstCompile] public void OnStartRunning(ref SystemState state) { }

        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
            // Discard every pending producer stream and main-thread inbox when leaving gameplay.
            owned.Inhibits.BeginRead(ref state); owned.Inhibits.EndRead(state.Dependency);
            owned.StatModifiers.BeginRead(ref state); owned.StatModifiers.EndRead(state.Dependency);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            owned.Inhibits.BeginTick(tick); owned.StatModifiers.BeginTick(tick);
            inhibited.Update(ref state); instances.Update(ref state); modifierBuffers.Update(ref state);
            var inhibitSet = owned.Inhibits.BeginRead(ref state);
            var modifierSet = owned.StatModifiers.BeginRead(ref state);
            var cleared = new ClearRequestsJob { Inhibits = inhibits, Modifiers = modifiers }.Schedule(state.Dependency);
            var gatheredInhibits = SimEventGather.Schedule(inhibitSet, inhibits, cleared);
            owned.Inhibits.EndRead(gatheredInhibits);
            var gatheredModifiers = SimEventGather.Schedule(modifierSet, modifiers, cleared);
            owned.StatModifiers.EndRead(gatheredModifiers);
            var applyInhibits = new ApplyInhibitsJob
            {
                Tick = tick, Requests = inhibits, Ordered = sortedInhibits, Inhibited = inhibited, Instances = instances
            }.Schedule(gatheredInhibits);
            var applyModifiers = new ApplyModifiersJob
            {
                Tick = tick, Requests = modifiers, Ordered = sortedModifiers, Buffers = modifierBuffers
            }.Schedule(gatheredModifiers);
            state.Dependency = new ExpireModifiersJob { Tick = tick }.ScheduleParallel(JobHandle.CombineDependencies(applyInhibits, applyModifiers));
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            owned.Inhibits.Dispose(); owned.StatModifiers.Dispose();
            if (inhibits.IsCreated) inhibits.Dispose(); if (modifiers.IsCreated) modifiers.Dispose();
            if (sortedInhibits.IsCreated) sortedInhibits.Dispose(); if (sortedModifiers.IsCreated) sortedModifiers.Dispose();
            if (state.EntityManager.Exists(channelEntity)) state.EntityManager.DestroyEntity(channelEntity);
        }

        struct OrderedInhibit { public InhibitRequest Request; public int Order; }
        struct OrderedModifier { public StatModifierRequest Request; public int Order; }
        struct InhibitOrder : IComparer<OrderedInhibit>
        {
            public int Compare(OrderedInhibit a, OrderedInhibit b)
            {
                int c = a.Request.Target.Index.CompareTo(b.Request.Target.Index); if (c != 0) return c;
                c = ((byte)a.Request.Producer).CompareTo((byte)b.Request.Producer); if (c != 0) return c;
                c = a.Request.Sequence.CompareTo(b.Request.Sequence); return c != 0 ? c : a.Order.CompareTo(b.Order);
            }
        }
        struct ModifierOrder : IComparer<OrderedModifier>
        {
            public int Compare(OrderedModifier a, OrderedModifier b)
            {
                int c = a.Request.Root.Index.CompareTo(b.Request.Root.Index); if (c != 0) return c;
                c = ((byte)a.Request.Producer).CompareTo((byte)b.Request.Producer); if (c != 0) return c;
                c = a.Request.SourceId.CompareTo(b.Request.SourceId); if (c != 0) return c;
                c = ((byte)a.Request.Stat).CompareTo((byte)b.Request.Stat); if (c != 0) return c;
                c = a.Request.Sequence.CompareTo(b.Request.Sequence); return c != 0 ? c : a.Order.CompareTo(b.Order);
            }
        }

        [BurstCompile]
        struct ClearRequestsJob : IJob
        {
            public NativeList<InhibitRequest> Inhibits;
            public NativeList<StatModifierRequest> Modifiers;
            public void Execute() { Inhibits.Clear(); Modifiers.Clear(); }
        }

        [BurstCompile]
        struct ApplyInhibitsJob : IJob
        {
            public uint Tick;
            [ReadOnly] public NativeList<InhibitRequest> Requests;
            public NativeList<OrderedInhibit> Ordered;
            public ComponentLookup<Inhibited> Inhibited;
            [ReadOnly] public BufferLookup<BlockInstance> Instances;
            public void Execute()
            {
                Ordered.Clear();
                for (int i = 0; i < Requests.Length; i++) Ordered.Add(new OrderedInhibit { Request = Requests[i], Order = i });
                Ordered.AsArray().Sort(new InhibitOrder());
                foreach (var entry in Ordered)
                {
                    var r = entry.Request;
                    uint until = r.DurationTicks > uint.MaxValue - Tick ? uint.MaxValue : Tick + r.DurationTicks;
                    if (Inhibited.HasComponent(r.Target)) Apply(r.Target, until, r.Reason);
                    else if (Instances.HasBuffer(r.Target))
                        foreach (var instance in Instances[r.Target])
                            if (instance.Block != 0 && (instance.Flags & BlockInstanceFlags.Destroyed) == 0 && Inhibited.HasComponent(instance.Functional))
                                Apply(instance.Functional, until, r.Reason);
                }
            }
            void Apply(Entity target, uint until, byte reason)
            {
                var value = Inhibited[target]; value.UntilTick = math.max(value.UntilTick, until); value.Reason = reason;
                Inhibited[target] = value;
            }
        }

        [BurstCompile]
        struct ApplyModifiersJob : IJob
        {
            public uint Tick;
            [ReadOnly] public NativeList<StatModifierRequest> Requests;
            public NativeList<OrderedModifier> Ordered;
            public BufferLookup<ShipStatModifier> Buffers;
            public void Execute()
            {
                Ordered.Clear();
                for (int i = 0; i < Requests.Length; i++) Ordered.Add(new OrderedModifier { Request = Requests[i], Order = i });
                Ordered.AsArray().Sort(new ModifierOrder());
                foreach (var entry in Ordered)
                {
                    var r = entry.Request;
                    if (!Buffers.HasBuffer(r.Root) || (byte)r.Stat > (byte)StatId.TurnRate) continue;
                    var buffer = Buffers[r.Root]; int index = -1;
                    for (int i = 0; i < buffer.Length; i++)
                        if (buffer[i].Producer == r.Producer && buffer[i].SourceId == r.SourceId && buffer[i].Stat == r.Stat) { index = i; break; }
                    if (r.Multiplier == 1 || r.UntilTick != uint.MaxValue && r.UntilTick <= Tick)
                    { if (index >= 0) buffer.RemoveAt(index); continue; }
                    if (!math.isfinite(r.Multiplier) || r.Multiplier <= 0) continue;
                    var value = new ShipStatModifier { Producer = r.Producer, SourceId = r.SourceId, Stat = r.Stat, Multiplier = r.Multiplier, UntilTick = r.UntilTick };
                    if (index >= 0) buffer[index] = value; else buffer.Add(value);
                }
            }
        }

        [BurstCompile]
        partial struct ExpireModifiersJob : IJobEntity
        {
            public uint Tick;
            void Execute(DynamicBuffer<ShipStatModifier> modifiers)
            {
                // uint.MaxValue is the documented until-cancelled sentinel, including at the final representable clock tick.
                for (int i = 0; i < modifiers.Length;)
                    if (modifiers[i].UntilTick != uint.MaxValue && Tick >= modifiers[i].UntilTick) modifiers.RemoveAt(i);
                    else i++;
            }
        }
    }
}
