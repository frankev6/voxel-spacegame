using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Narrative.Beats;
using Game.Narrative.Clues;
using Game.Narrative.Quests;

namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §5.5: live owner reads and one notification/marker rebuild after narrative evaluation.</summary>
    public sealed class ShipLogQuery : IDisposable
    {
        readonly FactStore facts;
        readonly QuestService quests;
        readonly ClueService clues;
        readonly BeatService beats;
        readonly IReadOnlyList<QuestDefinition> questDefinitions;
        readonly IReadOnlyList<ClueDefinition> clueDefinitions;
        readonly IReadOnlyList<MysteryDefinition> mysteries;
        readonly Dictionary<string, string>[] logTexts;
        readonly int[] mysteryOrder;
        readonly int[][] mysteryClues, progressValues;
        readonly int[] progressStage, progressCount;
        readonly bool[] progressDirty;
        readonly ulong[] journalFlags;
        readonly string[] journalTexts;
        readonly int[] journalValues, journalSlots;
        readonly List<QuestObjectiveView> objectiveScratch = new(16);
        readonly List<NarrativeMarker> markers = new();
        readonly IReadOnlyList<NarrativeMarker> markerView;
        bool dirty = true, progressPending, disposed;
        public ShipLogQuery(ContentCatalog catalog, FactStore facts, QuestService quests, ClueService clues, BeatService beats)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.quests = quests ?? throw new ArgumentNullException(nameof(quests));
            this.clues = clues ?? throw new ArgumentNullException(nameof(clues));
            this.beats = beats ?? throw new ArgumentNullException(nameof(beats));
            questDefinitions = catalog.All<QuestDefinition>(); clueDefinitions = catalog.All<ClueDefinition>();
            mysteries = catalog.All<MysteryDefinition>();
            logTexts = new Dictionary<string, string>[questDefinitions.Count + 1];
            progressValues = new int[logTexts.Length][]; progressDirty = new bool[logTexts.Length];
            progressStage = new int[logTexts.Length]; progressCount = new int[logTexts.Length];
            for (int q = 1; q < logTexts.Length; q++)
            {
                logTexts[q] = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var entry in questDefinitions[q - 1].LogEntries) logTexts[q][entry.id] = entry.text ?? "";
                progressValues[q] = new int[16]; progressStage[q] = -1;
            }
            mysteryOrder = new int[mysteries.Count]; mysteryClues = new int[mysteries.Count][];
            for (int m = 0; m < mysteries.Count; m++)
            {
                mysteryOrder[m] = m; var required = mysteries[m].Requires;
                mysteryClues[m] = new int[required.Count];
                for (int c = 0; c < required.Count; c++) mysteryClues[m][c] = catalog.IndexOf<ClueDefinition>(required[c]);
            }
            Array.Sort(mysteryOrder, (a, b) =>
            { int order = mysteries[a].Order.CompareTo(mysteries[b].Order); return order != 0 ? order : string.CompareOrdinal(mysteries[a].IdString, mysteries[b].IdString); });
            journalFlags = new ulong[(facts.Catalog.Count + 63) / 64]; journalTexts = new string[facts.Catalog.Count];
            journalValues = new int[facts.Catalog.Count]; var journals = new List<int>();
            for (int slot = 0; slot < facts.Catalog.Count; slot++)
                if ((facts.Catalog.FlagsAt(slot) & FactFlags.Journal) != 0)
                { journalFlags[slot >> 6] |= 1UL << (slot & 63); journals.Add(slot); }
            journalSlots = journals.ToArray();
            Array.Sort(journalSlots, (a, b) => string.CompareOrdinal(facts.Catalog.IdAt(a).Name, facts.Catalog.IdAt(b).Name));
            markerView = markers.AsReadOnly();
            quests.QuestChanged += QuestChanged; quests.ReadModelInvalidated += ProgressInvalidated;
            clues.ClueChanged += ChangedIndex; clues.MysterySolved += ChangedIndex;
            beats.ShipLogAfterEvaluation += AfterEvaluation; EventBus<FactChanged>.Subscribe(OnFactChanged);
        }
        public uint Revision { get; private set; }
        public event Action Changed;
        public IReadOnlyList<NarrativeMarker> Markers => markerView;
        void ChangedIndex(int index) => dirty = true;
        void QuestChanged(int index) { dirty = true; ProgressInvalidated(index); }
        void ProgressInvalidated(int index)
        { if (index > 0 && index < progressDirty.Length) { progressDirty[index] = true; progressPending = true; } }
        void OnFactChanged(FactChanged change)
        {
            if ((uint)change.Slot < (uint)facts.Catalog.Count && (journalFlags[change.Slot >> 6] & (1UL << (change.Slot & 63))) != 0 &&
                change.Id.Equals(facts.Catalog.IdAt(change.Slot))) dirty = true;
        }
        void AfterEvaluation()
        {
            if (disposed) return;
            if (clues.ConsumeMarkersDirty()) dirty = true;
            if (progressPending)
            {
                progressPending = false;
                for (int q = 1; q < progressDirty.Length; q++)
                {
                    if (!progressDirty[q]) continue;
                    progressDirty[q] = false; quests.GetObjectives(q, objectiveScratch);
                    int stage = quests.GetStage(q);
                    if (progressStage[q] != stage || progressCount[q] != objectiveScratch.Count) dirty = true;
                    progressStage[q] = stage; progressCount[q] = objectiveScratch.Count;
                    for (int i = 0; i < objectiveScratch.Count; i++)
                    {
                        if (progressValues[q][i] != objectiveScratch[i].Progress) dirty = true;
                        progressValues[q][i] = objectiveScratch[i].Progress;
                    }
                }
            }
            if (!dirty) return;
            dirty = false;
            NarrativeMarkers.Rebuild(quests, clues, questDefinitions, clueDefinitions, markers);
            Revision++; Changed?.Invoke();
        }
        QuestInstance Latest(int q)
        { var instances = quests.InstancesOf(q); return instances.Count == 0 ? null : instances[instances.Count - 1]; }
        QuestEntryView View(int q, QuestInstance instance)
        {
            var definition = questDefinitions[q - 1];
            string stage = instance.Status == QuestStatus.Active && (uint)instance.Stage < (uint)definition.Stages.Count
                ? definition.Stages[instance.Stage].title ?? "" : "";
            return new QuestEntryView(q, definition.IdString, definition.Title ?? "", stage, definition.ShortDescription ?? "",
                instance.Status, quests.TrackedQuestIndex == q, instance.Instance);
        }
        public void GetQuests(bool completed, List<QuestEntryView> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into)); into.Clear();
            for (int q = 1; q <= questDefinitions.Count; q++)
            {
                var instance = Latest(q);
                if (instance == null || (completed ? instance.Status != QuestStatus.Completed && instance.Status != QuestStatus.Failed : instance.Status != QuestStatus.Active)) continue;
                var view = View(q, instance); int at = into.Count; into.Add(view);
                while (at > 0 && Latest(into[at - 1].QuestIndex).AcceptOrder > instance.AcceptOrder)
                { into[at] = into[at - 1]; at--; }
                into[at] = view;
            }
        }
        public void GetObjectives(int questIndex, List<QuestObjectiveView> into) => quests.GetObjectives(questIndex, into);
        public void GetLog(int questIndex, List<string> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into)); into.Clear();
            var instance = Latest(questIndex); if (instance == null) return;
            foreach (var id in instance.Log) if (logTexts[questIndex].TryGetValue(id, out string text)) into.Add(text);
        }
        public bool TryGetTracked(out QuestEntryView quest, out QuestObjectiveView currentObjective)
        {
            quest = default; currentObjective = default;
            int q = quests.TrackedQuestIndex; var instance = Latest(q);
            if (instance == null || instance.Status != QuestStatus.Active) return false;
            quest = View(q, instance); quests.GetObjectives(q, objectiveScratch);
            foreach (var objective in objectiveScratch) if (!objective.Done) { currentObjective = objective; break; }
            return true;
        }
        public void SetTracked(int questIndex) => quests.Track(questIndex);
        public void MarkClueSeen(int clueIndex) => clues.MarkSeen(clueIndex);
        public void GetClues(bool rumors, List<ClueEntryView> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into)); into.Clear();
            for (int c = 1; c <= clueDefinitions.Count; c++)
            {
                var definition = clueDefinitions[c - 1]; var rumor = definition as RumorDefinition;
                if (!clues.IsKnown(c) || (rumor != null) != rumors) continue;
                var view = new ClueEntryView(c, definition.IdString, definition.Title ?? "", definition.Body ?? "", definition.Mystery?.Title ?? "",
                    clues.OrdinalOf(c), !clues.IsSeen(c), rumor != null, clues.IsResolved(c), rumor?.Target?.Id ?? ContentId.None);
                int at = into.Count; into.Add(view);
                while (at > 0 && into[at - 1].Ordinal > view.Ordinal) { into[at] = into[at - 1]; at--; }
                into[at] = view;
            }
        }
        public void GetMysteries(List<(string title, int known, int required, bool solved, string solvedText)> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into)); into.Clear();
            foreach (int m in mysteryOrder)
            {
                int known = 0; foreach (int c in mysteryClues[m]) if (clues.IsKnown(c)) known++;
                var definition = mysteries[m]; bool solved = clues.IsSolved(m + 1);
                into.Add((definition.Title ?? "", known, mysteryClues[m].Length, solved, solved ? definition.SolvedText ?? "" : ""));
            }
        }
        public void GetJournal(List<JournalEntryView> into)
        {
            if (into == null) throw new ArgumentNullException(nameof(into)); into.Clear();
            foreach (int slot in journalSlots)
            {
                int value = facts.GetBySlot(slot); if (value == 0) continue;
                if (journalTexts[slot] == null || journalValues[slot] != value)
                {
                    string text = facts.Catalog.JournalTextAt(slot);
                    journalTexts[slot] = facts.Catalog.KindAt(slot) == FactKind.Int && value > 1
                        ? text + " ×" + value.ToString(CultureInfo.InvariantCulture) : text;
                    journalValues[slot] = value;
                }
                into.Add(new JournalEntryView(facts.Catalog.IdAt(slot).Name, journalTexts[slot], value));
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            quests.QuestChanged -= QuestChanged; quests.ReadModelInvalidated -= ProgressInvalidated;
            clues.ClueChanged -= ChangedIndex; clues.MysterySolved -= ChangedIndex;
            beats.ShipLogAfterEvaluation -= AfterEvaluation; EventBus<FactChanged>.Unsubscribe(OnFactChanged);
        }
    }
}
