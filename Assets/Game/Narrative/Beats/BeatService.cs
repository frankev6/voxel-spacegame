using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Narrative.Dialog;

namespace Game.Narrative.Beats
{
    /// <summary>SPEC-42 §5.2: deterministic story selection and dialog/effect lifecycle.</summary>
    public sealed class BeatService : INarrativeSystem, IDisposable
    {
        readonly ContentCatalog catalog;
        readonly FactStore facts;
        readonly ILogicContext context;
        readonly DialogService dialog;
        readonly Func<ContentHash, uint> authoredStarSystemValue;
        readonly Func<ContentHash, string> bodyTagOfTemplate;
        ulong campaignSeed;
        readonly IReadOnlyList<StoryBeatDefinition> beats;
        readonly int[] slots;
        readonly int completedSlot;
        readonly List<int>[] triggers = new List<int>[6];
        readonly Dictionary<string, List<int>> tags = new(StringComparer.Ordinal);
        readonly Dictionary<ulong, List<int>> hails = new();
        readonly List<int> interactAnywhere = new(), candidates = new(), tied = new();
        readonly HashSet<int> dirty = new(), completing = new();
        readonly DependencyIndex dependencies;
        readonly EffectRequirements requirements = new();
        internal readonly BeatQueue QueueState = new();
        internal readonly HashSet<(ContentId Beat, uint System, ulong Key)> PerBody = new();
        int current;
        bool currentIntroduction;
        LogicScope currentScope;
        bool abortRequested, completedByEffect, gameStartPending, restored, disposed;
        public BeatService(ContentCatalog catalog, FactStore facts, ILogicContext context, DialogService dialog,
            Func<ContentHash, uint> authoredStarSystemValue, Func<ContentHash, string> bodyTagOfTemplate, ulong campaignSeed)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            this.dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            this.authoredStarSystemValue = authoredStarSystemValue ?? throw new ArgumentNullException(nameof(authoredStarSystemValue));
            this.bodyTagOfTemplate = bodyTagOfTemplate ?? throw new ArgumentNullException(nameof(bodyTagOfTemplate));
            this.campaignSeed = campaignSeed;
            beats = catalog.All<StoryBeatDefinition>(); slots = new int[beats.Count + 1];
            completedSlot = facts.Catalog.SlotOf("sys_beats_completed");
            if (completedSlot < 0) throw new ArgumentException("Generated beat counter is missing.", nameof(facts));
            for (int t = 0; t < triggers.Length; t++) triggers[t] = new List<int>();
            var reads = new IReadOnlyList<LogicDependency>[slots.Length]; reads[0] = Array.Empty<LogicDependency>();
            for (int i = 1; i < slots.Length; i++)
            {
                var beat = beats[i - 1]; slots[i] = facts.Catalog.GeneratedSlot(FactFamily.Beat, beat.Id.Hash.Value);
                if (slots[i] < 0) throw new ArgumentException("Generated beat fact is missing: " + beat.IdString);
                reads[i] = Array.Empty<LogicDependency>();
                if (beat.Debug && !UnityEngine.Application.isEditor && !UnityEngine.Debug.isDebugBuild) continue;
                triggers[(int)beat.Trigger].Add(i);
                if (beat.Trigger == BeatTrigger.Condition)
                { var dependenciesForBeat = new List<LogicDependency>(); beat.Condition.CollectDependencies(dependenciesForBeat); reads[i] = dependenciesForBeat; }
                if (beat.Trigger == BeatTrigger.Interact)
                {
                    if (beat.Where.bodyTags.Length == 0 && beat.Where.starSystem != null) interactAnywhere.Add(i);
                    foreach (var tag in beat.Where.bodyTags)
                    { if (!tags.TryGetValue(tag, out var list)) tags.Add(tag, list = new List<int>()); if (!list.Contains(i)) list.Add(i); }
                }
                if (beat.Trigger == BeatTrigger.Hail && beat.Where.hailEncounter != null)
                {
                    ulong hash = beat.Where.hailEncounter.Id.Hash.Value;
                    if (!hails.TryGetValue(hash, out var list)) hails.Add(hash, list = new List<int>());
                    list.Add(i);
                }
            }
            foreach (var list in triggers) list.Sort(CompareId);
            foreach (var list in tags.Values) list.Sort(CompareId);
            foreach (var list in hails.Values) list.Sort(CompareId);
            triggers[(int)BeatTrigger.GameStart].Sort((a, b) =>
            { int order = beats[b - 1].Salience.CompareTo(beats[a - 1].Salience); return order == 0 ? CompareId(a, b) : order; });
            dependencies = new DependencyIndex(reads);
            // App constructs this after the presenter so terminal effects observe a closed screen.
            dialog.Ended += Ended;
        }
        int CompareId(int a, int b) => StringComparer.Ordinal.Compare(beats[a - 1].IdString, beats[b - 1].IdString);
        bool Valid(int index) => index > 0 && index < slots.Length;
        public int Order => 30;
        public int CurrentBeatIndex => current;
        public int QueuedCount => QueueState.Count;
        uint CurrentSystem()
        {
            int index = context.World.CurrentStarSystemIndex;
            var systems = catalog.All<AuthoredStarSystemDefinition>();
            return index > 0 && index <= systems.Count ? authoredStarSystemValue(systems[index - 1].Id.Hash) : 0;
        }
        static LogicScope Scope(int beat, in LogicScope source, LogicUsage usage) =>
            new(usage, source.QuestIndex, source.QuestInstance, beat, source.HasBody, source.Body);
        bool RepeatAllows(int index, uint system, ulong key)
        {
            var beat = beats[index - 1];
            if (beat.Repeat == BeatRepeat.Once && facts.GetBySlot(slots[index]) != 0) return false;
            return beat.Repeat != BeatRepeat.OncePerBody || !PerBody.Contains((beat.Id, system, key));
        }
        bool Eligible(int index, in LogicScope scope, uint system, ulong key, bool firstVisit)
        {
            var beat = beats[index - 1];
            if (!RepeatAllows(index, system, key) || (beat.FirstVisitOnly && !firstVisit) ||
                (beat.Where.starSystem != null && authoredStarSystemValue(beat.Where.starSystem.Id.Hash) != system)) return false;
            context.PushScope(Scope(index, scope, LogicUsage.BeatCondition));
            try { return beat.Condition.Evaluate(context); } finally { context.PopScope(); }
        }
        int Select(List<int> list, in LogicScope scope, uint system, ulong key, bool firstVisit)
        {
            tied.Clear(); int salience = int.MinValue; float total = 0;
            foreach (int index in list)
            {
                var beat = beats[index - 1];
                if (beat.Salience < salience || !Eligible(index, scope, system, key, firstVisit)) continue;
                if (beat.Salience > salience) { salience = beat.Salience; tied.Clear(); total = 0; }
                tied.Add(index); total += beat.Weight;
            }
            if (tied.Count == 0) return 0;
            if (tied.Count == 1) return tied[0];
            var rng = RngStreams.For(campaignSeed, RngPurpose.Narrative, StableHash.Combine((ulong)facts.GetBySlot(completedSlot), key));
            float target = rng.NextFloat() * total, sum = 0;
            foreach (int index in tied) { sum += beats[index - 1].Weight; if (sum > target) return index; }
            return tied[tied.Count - 1];
        }
        public bool OnBodyInteracted(in LogicBodyHandle body, bool playerCanTalk)
        {
            if (!playerCanTalk || current != 0 || dialog.IsActive) return false;
            candidates.Clear(); candidates.AddRange(interactAnywhere);
            string tag = bodyTagOfTemplate(body.Template);
            if (tag != null && tags.TryGetValue(tag, out var matching))
                foreach (int index in matching) if (!candidates.Contains(index)) candidates.Add(index);
            candidates.Sort(CompareId);
            var scope = new LogicScope(LogicUsage.BeatCondition, 0, 0, 0, true, body);
            int selected = Select(candidates, scope, body.StarSystem, body.Key, true);
            if (selected == 0) return false; Play(selected, scope); return true;
        }
        public bool OnHail(ContentHash encounter, ulong memberPersistentId, bool playerCanTalk)
        {
            if (!playerCanTalk || current != 0 || dialog.IsActive || !hails.TryGetValue(encounter.Value, out var matching)) return false;
            int selected = Select(matching, default, CurrentSystem(), memberPersistentId, true);
            if (selected == 0) return false; Play(selected, default); return true;
        }
        public void OnStarSystemEntered(uint starSystemValue, bool firstVisit)
        {
            int selected = Select(triggers[(int)BeatTrigger.StarSystemEntered], default, starSystemValue, starSystemValue, firstVisit);
            if (selected != 0) Enqueue(selected, default);
        }
        void Enqueue(int index, in LogicScope scope, bool first = false, bool introduction = false)
        { if (Valid(index)) QueueState.Add(new BeatQueue.Entry(index, Scope(index, scope, LogicUsage.BeatEffect), beats[index - 1].Trigger == BeatTrigger.Condition, introduction: introduction), first); }
        public void Queue(int beatIndex) => Enqueue(beatIndex, current == 0 ? default : currentScope, introduction: currentIntroduction);
        public void DebugPlay(int beatIndex)
        { if (!Valid(beatIndex)) throw new ArgumentOutOfRangeException(nameof(beatIndex)); if (current != 0 || dialog.IsActive) { Enqueue(beatIndex, default); return; } Play(beatIndex, default); }
        void Play(int index, in LogicScope scope, bool introduction = false)
        {
            currentIntroduction = introduction;
            current = index; currentScope = Scope(index, scope, LogicUsage.BeatEffect); abortRequested = completedByEffect = false;
            Apply(beats[index - 1].OnStart, currentScope);
            if (string.IsNullOrEmpty(beats[index - 1].YarnNode))
            { if (!abortRequested && !completedByEffect) CompleteInternal(index, currentScope); current = 0; currentIntroduction = false; return; }
            if (!dialog.TryStart(new DialogRequest(index, beats[index - 1].YarnNode, currentScope)))
            { Enqueue(index, currentScope, true, currentIntroduction); current = 0; currentIntroduction = false; }
        }
        void Apply(CompiledEffectList effects, in LogicScope scope)
        {
            context.PushScope(scope);
            try { if (effects != null && !effects.ApplyAll(context, requirements)) Trace(LogicTraceKind.EffectRejected, scope.BeatIndex); }
            finally { context.PopScope(); }
        }
        void Ended(DialogEnded ended)
        {
            if (current == 0 || ended.BeatIndex != current) return;
            try
            {
                if (ended.Outcome == DialogOutcome.Completed && !abortRequested && !completedByEffect) CompleteInternal(current, currentScope);
                current = 0;
                if (ended.PendingTerminal == null) return;
                context.PushScope(Scope(ended.BeatIndex, ended.Scope, LogicUsage.BeatEffect));
                try { ended.PendingTerminal.Apply(context); } finally { context.PopScope(); }
            }
            finally { currentIntroduction = false; }
        }
        void CompleteInternal(int index, in LogicScope scope)
        {
            if (!completing.Add(index)) { GameLog.Warn("Narrative", "Recursive beat completion refused: " + beats[index - 1].IdString); return; }
            try
            {
                facts.AddBySlot(slots[index], 1, FactWriteSource.Beat); facts.AddBySlot(completedSlot, 1, FactWriteSource.Beat);
                if (beats[index - 1].Repeat == BeatRepeat.OncePerBody) PerBody.Add((beats[index - 1].Id, scope.Body.StarSystem, scope.Body.Key));
                Apply(beats[index - 1].OnComplete, Scope(index, scope, LogicUsage.BeatEffect)); Trace(LogicTraceKind.BeatCompleted, index);
            }
            finally { completing.Remove(index); }
        }
        public void CompleteBeat(int beatIndex)
        {
            if (!Valid(beatIndex)) return;
            if (beatIndex == current) { if (completedByEffect) return; completedByEffect = true; }
            CompleteInternal(beatIndex, current == 0 ? default : currentScope);
        }
        public void AbortCurrent() { if (current != 0) abortRequested = true; }
        public void OnNewGame()
        {
            // App owners outlive campaigns. GameSession sets the seed before this lifecycle callback.
            campaignSeed = context.CampaignSeed;
            QueueState.Clear(); PerBody.Clear(); ResetForRestore(); gameStartPending = true;
        }
        internal void ResetForRestore()
        { current = 0; currentIntroduction = false; currentScope = default; gameStartPending = false; dirty.Clear(); completing.Clear(); abortRequested = completedByEffect = false; restored = false; }
        public void OnRestored() { campaignSeed = context.CampaignSeed; ResetForRestore(); restored = true; }
        public void Evaluate(IReadOnlyList<LogicDependency> changed, bool fullReevaluate)
        {
            if (changed != null) dependencies.Collect(changed, dirty);
            if (fullReevaluate || restored) foreach (int index in triggers[(int)BeatTrigger.Condition]) dirty.Add(index);
            restored = false;
            foreach (int index in triggers[(int)BeatTrigger.Condition])
                if (dirty.Remove(index) && index != current && !QueueState.Contains(index) && Eligible(index, default, CurrentSystem(), 0, true)) Enqueue(index, default);
        }
        public void AfterEvaluation(bool playerFree)
        {
            try
            {
                if (!playerFree || current != 0 || dialog.IsActive) return;
                if (gameStartPending)
                {
                    gameStartPending = false;
                    foreach (int index in triggers[(int)BeatTrigger.GameStart])
                        if (Eligible(index, default, CurrentSystem(), 0, true)) Enqueue(index, default, introduction: true);
                }
                if (QueueState.TryPop(out var entry)) Play(entry.Beat, entry.Scope, entry.Introduction);
            }
            finally { ShipLogAfterEvaluation?.Invoke(); }
        }
        internal event Action ShipLogAfterEvaluation;
        void Trace(LogicTraceKind kind, int index) => facts.Trace?.Add(new LogicTraceEntry
        { Frame = UnityEngine.Time.frameCount, UniversalTime = context.World.UniversalTime, Kind = kind, Source = FactWriteSource.Beat, Subject = index });
        public void Dispose() { if (disposed) return; disposed = true; dialog.Ended -= Ended; }
    }
}
