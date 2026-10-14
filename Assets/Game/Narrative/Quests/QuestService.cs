using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;

namespace Game.Narrative.Quests
{
    /// <summary>SPEC-42 §5.3: ordered quest instances, captured baselines and effect-free restore evaluation.</summary>
    public sealed partial class QuestService : INarrativeSystem
    {
        readonly FactStore facts;
        readonly ILogicContext context;
        readonly NarrativeService host;
        readonly QuestDefinition[] definitions;
        readonly List<QuestInstance>[] instances;
        readonly int[][] baselineSlots;
        readonly DependencyIndex dependencies;
        readonly HashSet<int> dirty = new HashSet<int>();
        readonly EffectRequirements requirements = new EffectRequirements();
        int acceptCounter;
        bool restoring;
        public QuestService(ContentCatalog catalog, FactStore facts, ILogicContext context, NarrativeService host)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            var authored = catalog.All<QuestDefinition>();
            definitions = new QuestDefinition[authored.Count + 1];
            instances = new List<QuestInstance>[definitions.Length]; baselineSlots = new int[definitions.Length][];
            var owners = new IReadOnlyList<LogicDependency>[definitions.Length]; owners[0] = Array.Empty<LogicDependency>();
            foreach (var definition in authored)
            {
                int q = catalog.IndexOf(definition); definitions[q] = definition; instances[q] = new List<QuestInstance>();
                var reads = new List<LogicDependency>(); definition.FailWhen?.CollectDependencies(reads);
                foreach (var stage in definition.Stages)
                    foreach (var objective in stage.objectives)
                    { objective.doneWhen?.CollectDependencies(reads); objective.progress?.CollectDependencies(reads); }
                owners[q] = reads;
                var slots = new SortedSet<int>();
                foreach (var read in reads) if (read.Kind == LogicDependencyKind.Fact)
                {
                    if ((uint)read.Key >= (uint)facts.Catalog.Count) throw new ArgumentException("Quest fact dependencies must be bound.", nameof(catalog));
                    slots.Add(read.Key);
                }
                baselineSlots[q] = new int[slots.Count]; slots.CopyTo(baselineSlots[q]);
            }
            dependencies = new DependencyIndex(owners);
        }
        public int Order => 10;
        public int TrackedQuestIndex { get; private set; }
        public event Action<int> QuestChanged;
        internal event Action<int> ReadModelInvalidated;
        bool Valid(int q) => q > 0 && q < definitions.Length && definitions[q] != null;
        QuestInstance Latest(int q) => Valid(q) && instances[q].Count != 0 ? instances[q][instances[q].Count - 1] : null;
        public QuestStatus GetStatus(int questIndex) => Latest(questIndex)?.Status ?? QuestStatus.NotStarted;
        public int GetStage(int questIndex) => Latest(questIndex) is QuestInstance i && i.Status == QuestStatus.Active ? i.Stage : -1;
        public int GetInstanceCount(int questIndex) => Valid(questIndex) ? instances[questIndex].Count : 0;
        internal IReadOnlyList<QuestInstance> InstancesOf(int questIndex) => Valid(questIndex) ? instances[questIndex] : Array.Empty<QuestInstance>();
        public bool IsObjectiveDone(int questIndex, string objectiveId)
        {
            var instance = Latest(questIndex);
            if (instance == null || (instance.Status != QuestStatus.Active && instance.Status != QuestStatus.Completed)) return false;
            var stages = definitions[questIndex].Stages;
            for (int stage = 0; stage < stages.Count; stage++)
                for (int i = 0; i < stages[stage].objectives.Count; i++)
                    if (stages[stage].objectives[i].id == objectiveId)
                        return instance.Status == QuestStatus.Completed || stage < instance.Stage ||
                            stage == instance.Stage && (instance.DoneBits & (1ul << i)) != 0;
            return false;
        }
        public int GetBaseline(in LogicScope scope, int factSlot, BaselineKind kind)
        {
            if ((uint)factSlot >= (uint)facts.Catalog.Count)
            {
                GameLog.ErrorOnce("QuestBaseline:unknown:" + factSlot, "Narrative", "Unresolvable quest baseline fact; using zero.");
                return 0;
            }
            if (Valid(scope.QuestIndex) && (uint)scope.QuestInstance < (uint)instances[scope.QuestIndex].Count)
            {
                var i = instances[scope.QuestIndex][scope.QuestInstance];
                var slots = kind == BaselineKind.Accept ? i.AcceptBaselineSlots : i.StageBaselineSlots;
                var values = kind == BaselineKind.Accept ? i.AcceptBaselineValues : i.StageBaselineValues;
                int at = slots == null ? -1 : Array.IndexOf(slots, factSlot);
                if (at >= 0 && values != null && at < values.Length) return values[at];
            }
            GameLog.ErrorOnce("QuestBaseline:" + scope.QuestIndex + ":" + scope.QuestInstance + ":" + factSlot,
                "Narrative", "Quest baseline missing; use the current fact value to fail closed.");
            return facts.GetBySlot(factSlot);
        }
        int[] Capture(int[] slots)
        {
            var values = new int[slots.Length];
            for (int n = 0; n < slots.Length; n++) values[n] = facts.GetBySlot(slots[n]);
            return values;
        }
        static LogicScope Scope(QuestInstance i, LogicUsage usage) => new LogicScope(usage, i.QuestIndex, i.Instance, 0, false, default);
        void Apply(QuestInstance i, CompiledEffectList effects)
        {
            if (effects == null || effects.IsEmpty && !effects.HasErrors) return;
            context.PushScope(Scope(i, LogicUsage.QuestEffect));
            try { if (effects.ApplyAll(context, requirements) && !effects.IsEmpty) LogicEffectTrace.Applied(context); }
            finally { context.PopScope(); }
        }
        void Trace(QuestInstance i, LogicTraceKind kind, int oldValue = 0, int newValue = 0)
            => facts.Trace?.Add(new LogicTraceEntry { Frame = UnityEngine.Time.frameCount, UniversalTime = context.World.UniversalTime,
                Kind = kind, Source = FactWriteSource.Quest, Subject = i.QuestIndex, OldValue = oldValue, NewValue = newValue,
                ContextId = (ulong)(uint)i.Instance });
        void Changed(int q) { host.NotifyDependency(new LogicDependency(LogicDependencyKind.Quest, q)); QuestChanged?.Invoke(q); }
        public bool Start(int questIndex)
        {
            if (!Valid(questIndex)) return false;
            var definition = definitions[questIndex];
            foreach (var raw in rawSaved) if (raw.Id == definition.Id) return false;
            var prior = Latest(questIndex);
            if (prior != null && (prior.Status == QuestStatus.Active || !definition.Repeatable)) return false;
            var slots = baselineSlots[questIndex];
            var instance = new QuestInstance { QuestIndex = questIndex, Instance = instances[questIndex].Count,
                Status = QuestStatus.Active, Stage = 0, AcceptOrder = checked(++acceptCounter),
                AcceptBaselineSlots = slots, AcceptBaselineValues = Capture(slots),
                StageBaselineSlots = slots, StageBaselineValues = Capture(slots) };
            instances[questIndex].Add(instance);
            Apply(instance, definition.OnAccepted);
            if (instance.Status == QuestStatus.Active && (definition.AutoTrack || TrackedQuestIndex == 0)) Track(questIndex);
            dirty.Add(questIndex); Changed(questIndex); Trace(instance, LogicTraceKind.QuestAccepted);
            return true;
        }
        bool Finish(int q, QuestStatus status)
        {
            var i = Latest(q); if (i == null || i.Status != QuestStatus.Active) return false;
            i.Status = status;
            Apply(i, status == QuestStatus.Completed ? definitions[q].OnCompleted : definitions[q].OnFailed);
            // Nested effects may start the next repeatable instance; do not untrack that new active quest.
            if (TrackedQuestIndex == q && GetStatus(q) != QuestStatus.Active)
            {
                int candidate = 0, order = -1;
                for (int other = 1; other < definitions.Length; other++)
                {
                    var active = Latest(other);
                    if (active != null && active.Status == QuestStatus.Active && active.AcceptOrder > order)
                    { candidate = other; order = active.AcceptOrder; }
                }
                Track(candidate);
            }
            Changed(q); Trace(i, status == QuestStatus.Completed ? LogicTraceKind.QuestCompleted : LogicTraceKind.QuestFailed);
            return true;
        }
        public bool Fail(int questIndex) => Finish(questIndex, QuestStatus.Failed);
        public bool ForceComplete(int questIndex) => Finish(questIndex, QuestStatus.Completed);
        public void Track(int questIndex)
        {
            if (questIndex != 0 && GetStatus(questIndex) != QuestStatus.Active) return;
            unknownTracked = default;
            if (TrackedQuestIndex == questIndex) return;
            int old = TrackedQuestIndex; TrackedQuestIndex = questIndex;
            if (old != 0) QuestChanged?.Invoke(old);
            if (questIndex != 0) QuestChanged?.Invoke(questIndex);
        }
        public void AddLog(int questIndex, string entryId)
        {
            var i = Latest(questIndex); if (i == null) return;
            foreach (var entry in definitions[questIndex].LogEntries)
                if (entry.id == entryId) { i.Log.Add(entryId); QuestChanged?.Invoke(questIndex); return; }
            GameLog.WarnOnce("QuestLog:" + questIndex + ":" + entryId, "Narrative", "Unknown quest log entry: " + entryId);
        }
        public void GetObjectives(int questIndex, List<QuestObjectiveView> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into)); into.Clear();
            var i = Latest(questIndex); if (i == null || i.Status != QuestStatus.Active) return;
            context.PushScope(Scope(i, LogicUsage.QuestObjective));
            try
            {
                var objectives = definitions[questIndex].Stages[i.Stage].objectives;
                for (int n = 0; n < objectives.Count; n++)
                {
                    var objective = objectives[n];
                    into.Add(new QuestObjectiveView(objective.id, objective.text, (i.DoneBits & (1ul << n)) != 0,
                        objective.optional, objective.progress?.Evaluate(context) ?? 0, objective.target));
                }
            }
            finally { context.PopScope(); }
        }
        public void OnNewGame()
        {
            for (int q = 1; q < instances.Length; q++) instances[q].Clear();
            dirty.Clear(); acceptCounter = 0; TrackedQuestIndex = 0; restoring = false; rawSaved.Clear(); unknownTracked = default;
        }
        public void OnRestored() { restoring = true; for (int q = 1; q < definitions.Length; q++) dirty.Add(q); }
        public void Evaluate(IReadOnlyList<LogicDependency> changed, bool fullReevaluate)
        {
            dependencies.Collect(changed, dirty);
            if (fullReevaluate) for (int q = 1; q < definitions.Length; q++) dirty.Add(q);
            bool restorePass = restoring;
            try
            {
                for (int q = 1; q < definitions.Length; q++)
                {
                    if (!dirty.Remove(q)) continue;
                    var i = Latest(q); if (i == null || i.Status != QuestStatus.Active) continue;
                    EvaluateQuest(i, restorePass);
                    ReadModelInvalidated?.Invoke(q);
                }
            }
            finally { restoring = false; }
        }
        void EvaluateQuest(QuestInstance i, bool restorePass)
        {
            int q = i.QuestIndex; var definition = definitions[q];
            context.PushScope(Scope(i, LogicUsage.QuestObjective));
            try
            {
                bool failed = !string.IsNullOrWhiteSpace(definition.FailWhen?.Source) && definition.FailWhen.Evaluate(context);
                // The first restored evaluation must not fail, mutate saved bits or advance stages.
                if (!restorePass && failed) { Fail(q); return; }
                for (int step = 0; step < definition.Stages.Count && i.Status == QuestStatus.Active && ReferenceEquals(i, Latest(q)); step++)
                {
                    var stage = definition.Stages[i.Stage]; ulong before = i.DoneBits, bits = before; bool complete = true;
                    for (int n = 0; n < stage.objectives.Count; n++)
                    {
                        var objective = stage.objectives[n]; ulong bit = 1ul << n;
                        bool done = objective.latch && (bits & bit) != 0 || objective.doneWhen.Evaluate(context);
                        if (done) bits |= bit; else bits &= ~bit;
                        if (!objective.optional && !done) complete = false;
                    }
                    if (restorePass) break;
                    i.DoneBits = bits;
                    if (before != bits) Changed(q);
                    if (!complete || i.Status != QuestStatus.Active || !ReferenceEquals(i, Latest(q))) break;
                    int completedStage = i.Stage; Apply(i, stage.onComplete);
                    Trace(i, LogicTraceKind.StageCompleted, completedStage, completedStage + 1);
                    if (i.Status != QuestStatus.Active || !ReferenceEquals(i, Latest(q))) break;
                    if (completedStage == definition.Stages.Count - 1) { Finish(q, QuestStatus.Completed); break; }
                    i.Stage++; i.DoneBits = 0; i.StageBaselineSlots = baselineSlots[i.QuestIndex]; i.StageBaselineNames = null; i.StageBaselineValues = Capture(i.StageBaselineSlots);
                    Changed(q);
                }
            }
            finally { context.PopScope(); }
        }
        public void AfterEvaluation(bool playerFree) { }
        // TODO(QST-007): the QUST participant owns importing serialized instances and unknown IDs.
    }
}
