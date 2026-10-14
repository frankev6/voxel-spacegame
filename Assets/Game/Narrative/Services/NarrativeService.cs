using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Logic;

namespace Game.Narrative
{
    /// <summary>SPEC-40 §5.3: ordered, bounded narrative change passes on the main thread.</summary>
    public sealed class NarrativeService
    {
        public const int MaxPassesPerTick = 8;
        readonly List<INarrativeSystem> systems = new();
        readonly List<LogicDependency> changed, external;
        readonly HashSet<LogicDependency> changedSet, externalSet;
        readonly List<int> slots;
        bool session, pendingFull, playerFree, ticking;
        public NarrativeService(FactStore facts, ILogicContext context)
        {
            Facts = facts ?? throw new ArgumentNullException(nameof(facts));
            Context = context ?? throw new ArgumentNullException(nameof(context));
            int capacity = checked(facts.Catalog.Count + 256);
            changed = new List<LogicDependency>(capacity); external = new List<LogicDependency>(capacity);
            changedSet = new HashSet<LogicDependency>(capacity); externalSet = new HashSet<LogicDependency>(capacity);
            slots = new List<int>(facts.Catalog.Count);
            facts.BindTraceWorld(context.World);
        }
        public FactStore Facts { get; }
        public ILogicContext Context { get; }
        public bool IsWorldReady { get; private set; }
        public bool PlayerCanTalk { get; private set; }
        public void Register(INarrativeSystem system)
        {
            if (system == null) throw new ArgumentNullException(nameof(system));
            if (session || ticking) throw new InvalidOperationException("Narrative systems register before the session starts.");
            if (systems.Contains(system)) throw new ArgumentException("Narrative system is already registered.", nameof(system));
            int index = 0;
            while (index < systems.Count && systems[index].Order <= system.Order) index++;
            systems.Insert(index, system); // Equal orders retain registration order.
        }
        public void BeginNewGame()
        {
            BeginSession(); Facts.ResetToDefaults();
            for (int i = 0; i < systems.Count; i++) systems[i].OnNewGame();
        }
        public void OnSessionRestored()
        {
            BeginSession();
            for (int i = 0; i < systems.Count; i++) systems[i].OnRestored();
        }
        void BeginSession()
        {
            if (ticking) throw new InvalidOperationException("Cannot replace a session during narrative evaluation.");
            changed.Clear(); changedSet.Clear(); external.Clear(); externalSet.Clear(); slots.Clear();
            session = true; pendingFull = true;
        }
        public void SetWorldReady(bool ready) => IsWorldReady = ready;
        public void SetPlayerFree(bool free) => playerFree = free;
        public void SetPlayerCanTalk(bool canTalk) => PlayerCanTalk = canTalk;
        public void NotifyDependency(in LogicDependency dependency)
        {
            if (externalSet.Add(dependency)) external.Add(dependency);
        }
        void DrainExternal()
        {
            for (int i = 0; i < external.Count; i++) AddChanged(external[i]);
            external.Clear(); externalSet.Clear();
        }
        void AddChanged(in LogicDependency dependency)
        {
            if (changedSet.Add(dependency)) changed.Add(dependency);
        }
        public void Tick()
        {
            if (!session || !IsWorldReady) return;
            if (ticking) throw new InvalidOperationException("Narrative evaluation cannot reenter Tick.");
            ticking = true;
            try
            {
                changed.Clear(); changedSet.Clear(); DrainExternal();
                for (int pass = 0; pass < MaxPassesPerTick; pass++)
                {
                    slots.Clear(); Facts.DrainChanged(slots);
                    for (int i = 0; i < slots.Count; i++) AddChanged(new LogicDependency(LogicDependencyKind.Fact, slots[i]));
                    DrainExternal();
                    if (changed.Count == 0 && !pendingFull) break;
                    bool full = pendingFull; pendingFull = false;
                    for (int i = 0; i < systems.Count; i++) systems[i].Evaluate(changed, full);
                    changed.Clear(); changedSet.Clear();
                    if (pass + 1 != MaxPassesPerTick) continue;
                    // Effects from the last pass remain in FactStore/external until the next frame.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Facts.Trace?.Add(new LogicTraceEntry { Frame = UnityEngine.Time.frameCount,
                        UniversalTime = Context.World?.UniversalTime ?? 0, Kind = LogicTraceKind.PassLimitHit });
#endif
                    GameLog.WarnOnce("Narrative.PassLimit", "Narrative", "Evaluation did not settle in 8 passes; continuing next frame.");
                }
                for (int i = 0; i < systems.Count; i++) systems[i].AfterEvaluation(playerFree);
            }
            finally { ticking = false; }
        }
    }
}
