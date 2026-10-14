using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;

namespace Game.Narrative.Clues
{
    /// <summary>SPEC-42 §5.4: fact-owned discovery order and mysteries, latched rumors and ship-log seen state.</summary>
    public sealed class ClueService : INarrativeSystem
    {
        readonly FactStore facts;
        readonly ILogicContext context;
        readonly IReadOnlyList<ClueDefinition> clues;
        readonly IReadOnlyList<MysteryDefinition> mysteries;
        readonly int counterSlot;
        readonly int[] clueSlots, mysterySlots;
        readonly int[][] requiredClues;
        readonly List<int>[] requiringMysteries;
        readonly DependencyIndex dependencies;
        readonly HashSet<int> dirty;
        readonly HashSet<ContentId> seenClues = new(), resolvedRumors = new();
        readonly EffectRequirements effects = new();
        bool restoredEvaluation;
        internal bool MarkersDirty { get; private set; } = true;

        public ClueService(ContentCatalog catalog, FactStore facts, ILogicContext context)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            clues = catalog.All<ClueDefinition>(); mysteries = catalog.All<MysteryDefinition>();
            counterSlot = facts.Catalog.SlotOf("sys_clue_counter");
            if (counterSlot < 0) throw new ArgumentException("Generated clue counter is missing.", nameof(facts));
            clueSlots = new int[clues.Count + 1]; mysterySlots = new int[mysteries.Count + 1];
            requiringMysteries = new List<int>[clueSlots.Length]; requiredClues = new int[mysterySlots.Length][];
            var owners = new IReadOnlyList<LogicDependency>[clueSlots.Length]; owners[0] = Array.Empty<LogicDependency>();
            for (int i = 1; i < clueSlots.Length; i++)
            {
                clueSlots[i] = GeneratedSlot(FactFamily.Clue, clues[i - 1].Id);
                var reads = new List<LogicDependency>();
                if (clues[i - 1] is RumorDefinition rumor)
                {
                    rumor.ResolvedWhen?.CollectDependencies(reads);
                    reads.Add(new LogicDependency(LogicDependencyKind.Fact, clueSlots[i]));
                }
                owners[i] = reads;
            }
            for (int m = 1; m < mysterySlots.Length; m++)
            {
                var definition = mysteries[m - 1]; mysterySlots[m] = GeneratedSlot(FactFamily.Mystery, definition.Id);
                if (definition.Requires == null || definition.Requires.Count == 0) throw new ArgumentException("Mystery has no required clues.", nameof(catalog));
                requiredClues[m] = new int[definition.Requires.Count];
                for (int i = 0; i < definition.Requires.Count; i++)
                {
                    int c = catalog.IndexOf<ClueDefinition>(definition.Requires[i]);
                    if (!ValidClue(c)) throw new ArgumentException("Mystery requires a clue outside the catalog.", nameof(catalog));
                    requiredClues[m][i] = c;
                    requiringMysteries[c] ??= new List<int>();
                    if (!requiringMysteries[c].Contains(m)) requiringMysteries[c].Add(m);
                }
            }
            dependencies = new DependencyIndex(owners); dirty = new HashSet<int>(clues.Count);
        }
        int GeneratedSlot(FactFamily family, ContentId id)
        {
            int slot = facts.Catalog.GeneratedSlot(family, id.Hash.Value);
            if (slot < 0) throw new ArgumentException("Generated fact is missing for " + id.Value + ".", nameof(facts));
            return slot;
        }
        bool ValidClue(int index) => index > 0 && index < clueSlots.Length;
        bool ValidMystery(int index) => index > 0 && index < mysterySlots.Length;
        public int Order => 20;
        public bool IsKnown(int clueIndex) => ValidClue(clueIndex) && facts.GetBySlot(clueSlots[clueIndex]) != 0;
        public int OrdinalOf(int clueIndex) => ValidClue(clueIndex) ? facts.GetBySlot(clueSlots[clueIndex]) : 0;
        public bool IsSolved(int mysteryIndex) => ValidMystery(mysteryIndex) && facts.GetBySlot(mysterySlots[mysteryIndex]) != 0;
        public bool IsResolved(int rumorClueIndex) => ValidClue(rumorClueIndex) && clues[rumorClueIndex - 1] is RumorDefinition && resolvedRumors.Contains(clues[rumorClueIndex - 1].Id);
        public bool IsSeen(int clueIndex) => ValidClue(clueIndex) && seenClues.Contains(clues[clueIndex - 1].Id);
        public event Action<int> ClueChanged;
        public event Action<int> MysterySolved;

        public bool Reveal(int clueIndex)
        {
            if (!ValidClue(clueIndex) || IsKnown(clueIndex)) return false;
            int ordinal = checked(facts.GetBySlot(counterSlot) + 1);
            if (ordinal <= 0) throw new InvalidOperationException("Clue counter must produce a positive ordinal.");
            facts.SetBySlot(counterSlot, ordinal, FactWriteSource.Clue);
            facts.SetBySlot(clueSlots[clueIndex], ordinal, FactWriteSource.Clue);
            if (clues[clueIndex - 1] is RumorDefinition) dirty.Add(clueIndex);
            MarkersDirty = true; ClueChanged?.Invoke(clueIndex); Trace(LogicTraceKind.ClueRevealed, clueIndex, ordinal);
            var requiring = requiringMysteries[clueIndex];
            if (requiring != null)
                for (int i = 0; i < requiring.Count; i++)
                {
                    int m = requiring[i]; if (IsSolved(m)) continue;
                    bool allKnown = true;
                    foreach (int c in requiredClues[m]) if (!IsKnown(c)) { allKnown = false; break; }
                    if (!allKnown) continue;
                    // Latch before applying effects so nested reveals cannot solve this mystery again.
                    facts.SetBySlot(mysterySlots[m], 1, FactWriteSource.Clue);
                    var previous = context.Scope;
                    context.PushScope(new LogicScope(LogicUsage.QuestEffect, 0, 0, 0, previous.HasBody, previous.Body));
                    try { mysteries[m - 1].OnSolved?.ApplyAll(context, effects); }
                    finally { context.PopScope(); }
                    MysterySolved?.Invoke(m); Trace(LogicTraceKind.MysterySolved, m, 1);
                }
            return true;
        }
        public void MarkSeen(int clueIndex)
        {
            if (!IsKnown(clueIndex) || !seenClues.Add(clues[clueIndex - 1].Id)) return;
            ClueChanged?.Invoke(clueIndex);
        }
        public void OnNewGame()
        {
            seenClues.Clear(); resolvedRumors.Clear(); dirty.Clear(); restoredEvaluation = false; MarkersDirty = true;
            MarkKnownRumors();
        }
        public void OnRestored() { restoredEvaluation = true; MarkKnownRumors(); MarkersDirty = true; }
        void MarkKnownRumors()
        {
            for (int i = 1; i < clueSlots.Length; i++) if (clues[i - 1] is RumorDefinition && IsKnown(i)) dirty.Add(i);
        }
        public void Evaluate(IReadOnlyList<LogicDependency> changed, bool fullReevaluate)
        {
            if (fullReevaluate || restoredEvaluation) MarkKnownRumors();
            if (changed != null) dependencies.Collect(changed, dirty);
            bool silent = restoredEvaluation;
            for (int i = 1; i < clueSlots.Length && dirty.Count != 0; i++)
            {
                if (!dirty.Remove(i) || !IsKnown(i) || clues[i - 1] is not RumorDefinition rumor || resolvedRumors.Contains(rumor.Id)) continue;
                // The generic empty condition is true; the rumor contract explicitly makes empty source never resolve.
                if (rumor.ResolvedWhen == null || string.IsNullOrWhiteSpace(rumor.ResolvedWhen.Source) || !rumor.ResolvedWhen.Evaluate(context)) continue;
                resolvedRumors.Add(rumor.Id); MarkersDirty = true;
                if (!silent) ClueChanged?.Invoke(i);
            }
            restoredEvaluation = false;
        }
        public void AfterEvaluation(bool playerFree) { } // TODO(QST-006): marker composition belongs to the ship-log owner; no dialog pumping here.
        internal bool ConsumeMarkersDirty() { bool result = MarkersDirty; MarkersDirty = false; return result; }
        internal void CaptureState(List<ContentId> seen, List<ContentId> resolved)
        {
            if (seen == null || resolved == null || ReferenceEquals(seen, resolved)) throw new ArgumentException("Distinct output lists are required.");
            seen.Clear(); resolved.Clear();
            foreach (var id in seenClues) seen.Add(id);
            foreach (var id in resolvedRumors) resolved.Add(id);
            seen.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value)); resolved.Sort((a, b) => string.CompareOrdinal(a.Value, b.Value));
        }
        internal void RestoreState(IReadOnlyList<ContentId> seen, IReadOnlyList<ContentId> resolved)
        {
            if (seen == null || resolved == null) throw new ArgumentNullException(seen == null ? nameof(seen) : nameof(resolved));
            seenClues.Clear(); resolvedRumors.Clear(); dirty.Clear();
            for (int i = 0; i < seen.Count; i++) seenClues.Add(seen[i]);
            for (int i = 0; i < resolved.Count; i++) resolvedRumors.Add(resolved[i]);
            MarkersDirty = true;
        }
        void Trace(LogicTraceKind kind, int subject, int value)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            facts.Trace?.Add(new LogicTraceEntry { Frame = UnityEngine.Time.frameCount, UniversalTime = context.World.UniversalTime,
                Kind = kind, Source = FactWriteSource.Clue, Subject = subject, NewValue = value });
#endif
        }
    }
}
