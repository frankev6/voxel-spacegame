using Game.Core.Components;
using Game.Core.Systems;
using Game.Data.Blocks;
using Game.Data.Components;
using Game.Data.Hazards;
using Game.Simulation.Hazards;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Game.Simulation.Mechanisms
{
    /// <summary>HAZ-014 (ADR-0119 decision 2): the authority decider that connects triggers to actions. Each tick it reads what happened
    /// (switches damaged, block instances destroyed, gate and zone contacts, timers, the race leader's lap), fires the matching rules of
    /// every placed Mechanism, and writes the actions that fall due into MechanismActions.Current. It writes nothing else: each owner
    /// (MoverSystem, ZoneSystem, ...) applies the actions on its own state on its next update. Main thread; small by design.</summary>
    [BurstCompile, UpdateInGroup(typeof(GameplayPostPhysicsGroup))]
    [UpdateAfter(typeof(DamageResolveSystem)), UpdateAfter(typeof(ZoneSystem)), UpdateAfter(typeof(MineSystem))]
    public partial struct MechanismSystem : ISystem
    {
        struct Event { public MechanismTriggerKind Kind; public ushort Group; public float Value; }
        struct Pending : System.IComparable<Pending>
        {
            public MechanismAction Action; public uint Due; public uint Order;
            public int CompareTo(Pending b) => Due != b.Due ? Due.CompareTo(b.Due) : Order.CompareTo(b.Order);
        }

        Entity actionsEntity;
        NativeList<Pending> pending;
        NativeHashMap<ulong, float> switchHp;          // (root index, instance) → last function HP
        NativeHashSet<ulong> destroyed;                // (root index, instance) already reported destroyed
        NativeHashSet<ulong> insideBounds;             // (body index, zone id) inside last tick
        uint order; int lastLap;
        EntityQuery mechanisms, groups;

        public void OnCreate(ref SystemState state)
        {
            actionsEntity = state.EntityManager.CreateEntity(typeof(MechanismActions), typeof(RaceLapSignal));
            state.EntityManager.SetName(actionsEntity, "MechanismActions");
            state.EntityManager.SetComponentData(actionsEntity, new MechanismActions { Current = new NativeList<MechanismAction>(8, Allocator.Persistent) });
            pending = new NativeList<Pending>(16, Allocator.Persistent);
            switchHp = new NativeHashMap<ulong, float>(32, Allocator.Persistent);
            destroyed = new NativeHashSet<ulong>(32, Allocator.Persistent);
            insideBounds = new NativeHashSet<ulong>(32, Allocator.Persistent);
            mechanisms = SystemAPI.QueryBuilder().WithAll<Mechanism, MechanismRule, MechanismRuleAction>().Build();
            groups = SystemAPI.QueryBuilder().WithAll<MechanismGroup, BlockInstance>().Build();
            state.RequireForUpdate<GameplayActive>(); state.RequireForUpdate<SimClock>(); state.RequireForUpdate(mechanisms);
        }

        public void OnDestroy(ref SystemState state)
        {
            state.CompleteDependency();
            if (state.EntityManager.Exists(actionsEntity))
            {
                var a = state.EntityManager.GetComponentData<MechanismActions>(actionsEntity);
                if (a.Current.IsCreated) a.Current.Dispose();
            }
            if (pending.IsCreated) pending.Dispose(); if (switchHp.IsCreated) switchHp.Dispose();
            if (destroyed.IsCreated) destroyed.Dispose(); if (insideBounds.IsCreated) insideBounds.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // ZoneContacts and instance HP are written by jobs that do not declare them to this system (lists inside components,
            // the damage writer's buffers): wait for every job. This only runs where a mechanism is placed.
            state.EntityManager.CompleteAllTrackedJobs();
            var em = state.EntityManager;
            uint tick = SystemAPI.GetSingleton<SimClock>().Tick;
            var actions = em.GetComponentData<MechanismActions>(actionsEntity);
            actions.Current.Clear(); actions.Tick = tick;
            var events = new NativeList<Event>(8, Allocator.Temp);

            // Switches and instances of the roots that take part in a mechanism group.
            if (SystemAPI.TryGetSingleton<BlockModulesRef>(out var modulesRef))
            {
                ref var modules = ref modulesRef.Value.Value;
                using var roots = groups.ToEntityArray(Allocator.Temp);
                for (int r = 0; r < roots.Length; r++)
                {
                    var root = roots[r]; ushort group = em.GetComponentData<MechanismGroup>(root).Group;
                    var instances = em.GetBuffer<BlockInstance>(root, true);
                    for (int i = 0; i < instances.Length; i++)
                    {
                        var instance = instances[i]; if (instance.Block == 0) continue;
                        ulong key = ((ulong)(uint)root.Index << 32) | (uint)i;
                        bool gone = instance.CellsAlive == 0 || (instance.Flags & BlockInstanceFlags.Destroyed) != 0;
                        if (gone && destroyed.Add(key)) events.Add(new Event { Kind = MechanismTriggerKind.InstanceDestroyed, Group = group });
                        if (!modules.TryGet(BlockModuleId.Switch, instance.Block, out SwitchParams sw)) continue;
                        ushort switchGroup = sw.Group != 0 ? sw.Group : group;
                        if (switchHp.TryGetValue(key, out float last) && last - instance.FunctionHp >= math.max(sw.TriggerDamage, 1e-3f))
                            events.Add(new Event { Kind = MechanismTriggerKind.SwitchDamaged, Group = switchGroup });
                        switchHp[key] = instance.FunctionHp;
                    }
                }
            }

            // Gate crossings and zone entries from this tick's zone contacts.
            if (SystemAPI.TryGetSingleton<ZoneContacts>(out var contacts) && contacts.Current.IsCreated)
            {
                var nowInside = new NativeHashSet<ulong>(8, Allocator.Temp);
                foreach (var c in contacts.Current)
                {
                    if (c.Kind == ZoneEffectKind.Gate && c.Crossed != 0) events.Add(new Event { Kind = MechanismTriggerKind.GateCrossed, Value = c.Id });
                    if (c.Kind == ZoneEffectKind.Bounds && c.Inside != 0)
                    {
                        ulong key = ((ulong)(uint)c.Body.Index << 32) | c.Id; nowInside.Add(key);
                        if (!insideBounds.Contains(key)) events.Add(new Event { Kind = MechanismTriggerKind.ZoneEntered, Value = c.Id });
                    }
                }
                insideBounds.Clear(); foreach (var k in nowInside) insideBounds.Add(k);
                nowInside.Dispose();
            }

            // The race leader's lap, as the race reports it.
            int lap = em.GetComponentData<RaceLapSignal>(actionsEntity).LeaderLap;
            if (lap > lastLap) events.Add(new Event { Kind = MechanismTriggerKind.RaceLap, Value = lap });
            lastLap = lap;

            // Fire the rules, mechanisms in group order so every run agrees.
            using (var placed = mechanisms.ToEntityArray(Allocator.Temp))
            {
                var sorted = new NativeArray<Entity>(placed, Allocator.Temp);
                sorted.Sort(new ByGroup { Em = em });
                foreach (var m in sorted)
                {
                    ushort own = em.GetComponentData<Mechanism>(m).Group;
                    var rules = em.GetBuffer<MechanismRule>(m, true); var acts = em.GetBuffer<MechanismRuleAction>(m, true);
                    for (int r = 0; r < rules.Length; r++)
                    {
                        var rule = rules[r]; ushort group = rule.Group != 0 ? rule.Group : own;
                        int fires = 0;
                        if (rule.Kind == MechanismTriggerKind.Timer)
                        {
                            uint period = (uint)math.max(1, math.round(rule.Value * 60)), offset = (uint)math.max(0, math.round(rule.Offset * 60));
                            if (tick >= offset && (tick - offset) % period == 0) fires = 1;
                        }
                        else foreach (var e in events)
                        {
                            if (e.Kind != rule.Kind) continue;
                            bool match = rule.Kind == MechanismTriggerKind.SwitchDamaged || rule.Kind == MechanismTriggerKind.InstanceDestroyed
                                ? e.Group == group : rule.Kind == MechanismTriggerKind.RaceLap ? e.Value >= rule.Value && e.Value - 1 < rule.Value : e.Value == rule.Value;
                            if (match) fires++;
                        }
                        if (fires == 0) continue;   // several events of one kind in a tick fire a rule once
                        for (int a = rule.ActionStart; a < rule.ActionStart + rule.ActionCount && a < acts.Length; a++)
                        {
                            var act = acts[a];
                            pending.Add(new Pending { Due = tick + act.DelayTicks, Order = order++,
                                Action = new MechanismAction { Kind = act.Kind, Group = act.Group != 0 ? act.Group : own, Value = act.Value } });
                        }
                    }
                }
                sorted.Dispose();
            }
            events.Dispose();

            // Release what falls due.
            pending.Sort();
            int done = 0;
            while (done < pending.Length && pending[done].Due <= tick) { actions.Current.Add(pending[done].Action); done++; }
            if (done > 0) pending.RemoveRange(0, done);
            em.SetComponentData(actionsEntity, actions);
        }

        struct ByGroup : System.Collections.Generic.IComparer<Entity>
        {
            public EntityManager Em;
            public int Compare(Entity a, Entity b)
            {
                int c = Em.GetComponentData<Mechanism>(a).Group.CompareTo(Em.GetComponentData<Mechanism>(b).Group);
                return c != 0 ? c : a.Index.CompareTo(b.Index);
            }
        }
    }
}
