using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Narrative;
using Game.Narrative.ShipLog;
using Game.Narrative.Quests;
using Game.Narrative.Beats;
using Game.Narrative.Dialog;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.DevTools.Editor.Narrative
{
    /// <summary>SPEC-40 §5.9: live snapshots and explicit debugger actions through canonical owners.</summary>
    public sealed class NarrativeDebuggerWindow : EditorWindow
    {
        const string LayoutPath = "Assets/Game/DevTools/Editor/Narrative/NarrativeDebuggerWindow.uxml";
        const string ThemePath = "Assets/Game/UI/Theme/NarrativeDebugger.uss";
        const double RefreshSeconds = .5;
        readonly List<FactRow> facts = new();
        readonly List<string> traceRows = new(), questRows = new();
        readonly List<QuestEntryView> activeQuests = new(), finishedQuests = new();
        readonly LogicTraceEntry[] traceBuffer = new LogicTraceEntry[LogicTraceLog.Capacity];
        readonly List<string> baselineRows = new(), beatIds = new();
        readonly List<LogicDependency> baselineDependencies = new();
        readonly List<int> baselineSlots = new();
        readonly HashSet<int> baselineSet = new();
        MultiColumnListView factsView;
        ListView traceView, questsView, baselinesView;
        ToolbarSearchField search;
        Label status, factStatus, traceStatus, questStatus;
        VisualElement factsPanel, tracePanel, questsPanel;
        ToolbarToggle factsTab, traceTab, questsTab;
        ToolbarToggle baselinesTab, debugMenus;
        VisualElement baselinesPanel;
        Label selectedFact, editStatus, baselineStatus, playStatus;
        IntegerField factValue;
        Button applyFact, playBeat;
        DropdownField beatChoice;
        int selectedFactSlot = -1;
        bool factValueDirty, syncingFacts;
        string playMessage;
        NarrativeService observed;
        int traceVersion;
        bool wasPlaying;
        double refreshAt;

        readonly struct FactRow
        {
            internal readonly string Name, Family, Kind, Value, Default;
            internal readonly bool NonDefault;
            internal readonly int Slot;
            internal FactRow(Game.Data.Logic.FactCatalog catalog, int slot, int value)
            {
                Slot = slot; Name = catalog.IdAt(slot).Name; Family = catalog.FamilyAt(slot).ToString(); Kind = catalog.KindAt(slot).ToString();
                Value = value.ToString(CultureInfo.InvariantCulture); Default = catalog.DefaultAt(slot).ToString(CultureInfo.InvariantCulture);
                NonDefault = value != catalog.DefaultAt(slot);
            }
            internal string Cell(int column) => column switch { 0 => Name, 1 => Family, 2 => Kind, 3 => Value, _ => Default };
        }

        [MenuItem("Game/Narrative/Debugger")]
        static void Open() => GetWindow<NarrativeDebuggerWindow>("Narrative Debugger");

        void OnEnable()
        {
            refreshAt = 0;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }
        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            observed = null;
            baselineRows.Clear(); beatIds.Clear(); baselineDependencies.Clear(); baselineSlots.Clear(); baselineSet.Clear();
            facts.Clear(); traceRows.Clear(); questRows.Clear(); activeQuests.Clear(); finishedQuests.Clear();
            factsView = null;
        }
        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("narrative-debugger-root");
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Game/UI/Theme/tokens.uss"));
            rootVisualElement.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath));
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath).CloneTree(rootVisualElement);
            factsPanel = rootVisualElement.Q("facts-panel"); tracePanel = rootVisualElement.Q("trace-panel"); questsPanel = rootVisualElement.Q("quests-panel");
            status = rootVisualElement.Q<Label>("status"); factStatus = rootVisualElement.Q<Label>("fact-status");
            traceStatus = rootVisualElement.Q<Label>("trace-status"); questStatus = rootVisualElement.Q<Label>("quest-status");
            factsTab = rootVisualElement.Q<ToolbarToggle>("facts-tab"); traceTab = rootVisualElement.Q<ToolbarToggle>("trace-tab");
            questsTab = rootVisualElement.Q<ToolbarToggle>("quests-tab"); search = rootVisualElement.Q<ToolbarSearchField>("search");
            baselinesTab = rootVisualElement.Q<ToolbarToggle>("baselines-tab"); baselinesPanel = rootVisualElement.Q("baselines-panel");
            baselineStatus = rootVisualElement.Q<Label>("baseline-status"); selectedFact = rootVisualElement.Q<Label>("selected-fact");
            factValue = rootVisualElement.Q<IntegerField>("fact-value"); applyFact = rootVisualElement.Q<Button>("apply-fact");
            editStatus = rootVisualElement.Q<Label>("edit-status"); debugMenus = rootVisualElement.Q<ToolbarToggle>("debug-menus");
            beatChoice = rootVisualElement.Q<DropdownField>("beat-choice"); playBeat = rootVisualElement.Q<Button>("play-beat");
            playStatus = rootVisualElement.Q<Label>("play-status");
            factsTab.RegisterValueChangedCallback(_ => SelectTab(0)); traceTab.RegisterValueChangedCallback(_ => SelectTab(1));
            questsTab.RegisterValueChangedCallback(_ => SelectTab(2)); search.RegisterValueChangedCallback(_ => RefreshNow());
            baselinesTab.RegisterValueChangedCallback(_ => SelectTab(3));
            factValue.RegisterValueChangedCallback(_ => factValueDirty = true);
            debugMenus.RegisterValueChangedCallback(change => SetDebugMenus(change.newValue));
            applyFact.clicked += ApplySelectedFact; playBeat.clicked += PlaySelectedBeat;
            rootVisualElement.Q<ToolbarButton>("refresh").clicked += RefreshNow;
            factsView = new MultiColumnListView { itemsSource = facts, selectionType = SelectionType.Single };
            factsView.selectionChanged += _ => SelectFact();
            factsView.AddToClassList("narrative-debugger-list");
            string[] headers = { "Name", "Family", "Kind", "Value", "Default" };
            for (int i = 0; i < headers.Length; i++)
            {
                int column = i;
                factsView.columns.Add(new Column { name = headers[i], title = headers[i], stretchable = true, resizable = true,
                    makeCell = () => CellLabel(false), bindCell = (cell, row) =>
                    { var label = (Label)cell; label.text = facts[row].Cell(column); label.tooltip = label.text; } });
            }
            factsPanel.Add(factsView);
            traceView = TextList(traceRows); tracePanel.Add(traceView);
            questsView = TextList(questRows); questsPanel.Add(questsView);
            baselinesView = TextList(baselineRows); baselinesPanel.Add(baselinesView);
            SelectTab(0); RefreshNow();
        }
        static Label CellLabel(bool wrap)
        {
            var label = new Label(); label.AddToClassList(wrap ? "narrative-debugger-entry" : "narrative-debugger-cell");
            return label;
        }
        static ListView TextList(List<string> rows)
        {
            var list = new ListView { itemsSource = rows, selectionType = SelectionType.None,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                makeItem = () => CellLabel(true), bindItem = (element, i) => ((Label)element).text = rows[i] };
            list.AddToClassList("narrative-debugger-list"); return list;
        }
        void SelectTab(int tab)
        {
            factsTab.SetValueWithoutNotify(tab == 0); traceTab.SetValueWithoutNotify(tab == 1); questsTab.SetValueWithoutNotify(tab == 2);
            baselinesTab.SetValueWithoutNotify(tab == 3);
            factsPanel.EnableInClassList("narrative-debugger-hidden", tab != 0);
            tracePanel.EnableInClassList("narrative-debugger-hidden", tab != 1);
            questsPanel.EnableInClassList("narrative-debugger-hidden", tab != 2);
            baselinesPanel.EnableInClassList("narrative-debugger-hidden", tab != 3);
        }
        void OnEditorUpdate()
        {
            if (factsView == null) return;
            NarrativeService current = null;
            if (EditorApplication.isPlaying) Game.Core.Services.TryGet(out current);
            int version = current?.Facts.Trace?.Version ?? 0;
            if (!ReferenceEquals(current, observed) || wasPlaying != EditorApplication.isPlaying || version != traceVersion ||
                EditorApplication.timeSinceStartup >= refreshAt) Refresh(current);
        }
        void RefreshNow()
        {
            if (factsView == null) return;
            NarrativeService current = null;
            if (EditorApplication.isPlaying) Game.Core.Services.TryGet(out current);
            Refresh(current);
        }
        void Refresh(NarrativeService current)
        {
            if (!ReferenceEquals(current, observed)) { selectedFactSlot = -1; factValueDirty = false; playMessage = null; }
            observed = current; wasPlaying = EditorApplication.isPlaying;
            traceVersion = current?.Facts.Trace?.Version ?? 0; refreshAt = EditorApplication.timeSinceStartup + RefreshSeconds;
            facts.Clear(); traceRows.Clear(); questRows.Clear(); baselineRows.Clear(); activeQuests.Clear(); finishedQuests.Clear();
            if (current == null)
            {
                status.text = wasPlaying ? "NarrativeService is not registered in this Play session." : "Enter Play mode to inspect the live narrative.";
                factStatus.text = traceStatus.text = questStatus.text = "Unavailable until a live narrative service is registered.";
                baselineStatus.text = "Baselines require a live narrative and quest runtime.";
            }
            else
            {
                status.text = current.IsWorldReady ? "Live narrative · debugger actions write with Debugger source" : "Narrative available · world not ready";
                RefreshFacts(current.Facts);
                RefreshTrace(current.Facts);
                bool questsAvailable = RefreshQuests();
                RefreshBaselines(current.Facts, questsAvailable);
            }
            syncingFacts = true;
            try
            {
                factsView.RefreshItems();
                int selected = facts.FindIndex(row => row.Slot == selectedFactSlot);
                factsView.SetSelectionWithoutNotify(selected < 0 ? Array.Empty<int>() : new[] { selected });
                if (selected < 0) { selectedFactSlot = -1; factValueDirty = false; }
            }
            finally { syncingFacts = false; }
            traceView.RefreshItems(); questsView.RefreshItems(); baselinesView.RefreshItems();
            RefreshEditing(current); RefreshBeats(current);
        }
        void RefreshFacts(FactStore store)
        {
            string filter = search.value ?? "";
            for (int slot = 0; slot < store.Catalog.Count; slot++)
            {
                if (store.Catalog.IdAt(slot).Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                facts.Add(new FactRow(store.Catalog, slot, store.GetBySlot(slot)));
            }
            facts.Sort((a, b) => { int changed = b.NonDefault.CompareTo(a.NonDefault); return changed != 0 ? changed : StringComparer.Ordinal.Compare(a.Name, b.Name); });
            factStatus.text = string.Format(CultureInfo.InvariantCulture, "{0} / {1} facts · {2} non-default · non-default first",
                facts.Count, store.Catalog.Count, store.NonDefaultCount);
        }
        void RefreshTrace(FactStore store)
        {
            int count = store.Trace?.CopyTo(traceBuffer) ?? 0;
            for (int i = count - 1; i >= 0; i--) traceRows.Add(FormatTraceEntry(traceBuffer[i], SubjectName(traceBuffer[i], store)));
            traceStatus.text = store.Trace == null ? "Trace is not enabled in this session." :
                string.Format(CultureInfo.InvariantCulture, "{0} / {1} entries · newest first", count, LogicTraceLog.Capacity);
        }
        static string SubjectName(LogicTraceEntry entry, FactStore store)
        {
            if (entry.Kind == LogicTraceKind.FactWrite && (uint)entry.Subject < (uint)store.Catalog.Count)
                return store.Catalog.IdAt(entry.Subject).Name;
            if (entry.Kind == LogicTraceKind.PassLimitHit) return "narrative";
            if (entry.Subject > 0 && entry.Subject <= ushort.MaxValue && Game.Core.Services.TryGet<ContentService>(out var content))
            {
                ushort index = (ushort)entry.Subject;
                ContentDefinition definition = entry.Kind switch
                {
                    LogicTraceKind.BeatSelected or LogicTraceKind.BeatCompleted or LogicTraceKind.BeatAborted => content.Catalog.GetByIndex<StoryBeatDefinition>(index),
                    LogicTraceKind.QuestAccepted or LogicTraceKind.StageCompleted or LogicTraceKind.QuestCompleted or LogicTraceKind.QuestFailed => content.Catalog.GetByIndex<QuestDefinition>(index),
                    LogicTraceKind.ClueRevealed => content.Catalog.GetByIndex<ClueDefinition>(index),
                    LogicTraceKind.MysterySolved => content.Catalog.GetByIndex<MysteryDefinition>(index),
                    _ => null
                };
                if (definition != null) return definition.IdString;
            }
            return "subject:" + entry.Subject.ToString(CultureInfo.InvariantCulture);
        }
        bool RefreshQuests()
        {
            if (!Game.Core.Services.TryGet<ShipLogQuery>(out var query))
            { questStatus.text = "ShipLogQuery is not registered (QST-006)."; return false; }
            activeQuests.Clear(); finishedQuests.Clear();
            query.GetQuests(false, activeQuests); query.GetQuests(true, finishedQuests);
            AppendQuests(activeQuests); AppendQuests(finishedQuests);
            questStatus.text = string.Format(CultureInfo.InvariantCulture, "{0} active · {1} completed or failed · read only", activeQuests.Count, finishedQuests.Count);
            return true;
        }
        void AppendQuests(List<QuestEntryView> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                var quest = rows[i];
                questRows.Add(string.Format(CultureInfo.InvariantCulture, "{0} · {1} · {2} · instance {3}{4}\n{5}\n{6}",
                    quest.Title, quest.QuestId, quest.Status, quest.Instance, quest.Tracked ? " · tracked" : "", quest.StageText, quest.ShortDescription));
            }
        }
        void SelectFact()
        {
            if (syncingFacts || observed == null) return;
            int row = factsView.selectedIndex;
            selectedFactSlot = row >= 0 && row < facts.Count ? facts[row].Slot : -1;
            factValueDirty = false;
            RefreshEditing(observed);
        }
        void RefreshEditing(NarrativeService current)
        {
            bool selected = current != null && (uint)selectedFactSlot < (uint)current.Facts.Catalog.Count;
            selectedFact.text = selected ? current.Facts.Catalog.IdAt(selectedFactSlot).Name : "No fact selected";
            factValue.SetEnabled(selected); applyFact.SetEnabled(selected);
            if (selected && !factValueDirty) factValue.SetValueWithoutNotify(current.Facts.GetBySlot(selectedFactSlot));
            int debugSlot = current?.Facts.Catalog.SlotOf("debug_menus") ?? -1;
            debugMenus.SetEnabled(debugSlot >= 0);
            debugMenus.SetValueWithoutNotify(debugSlot >= 0 && current.Facts.GetBySlot(debugSlot) != 0);
            debugMenus.tooltip = debugSlot >= 0 ? "Writes debug_menus through FactStore with Debugger source." : "debug_menus is unavailable in this runtime fact catalog.";
            editStatus.text = current == null ? "Fact editing requires a live Play session." : "Select a fact and set its value · Debugger source · runtime clamping applies";
        }
        bool TryLive(out NarrativeService current)
        {
            current = null;
            return EditorApplication.isPlaying && Game.Core.Services.TryGet(out current) && ReferenceEquals(current, observed);
        }
        void ApplySelectedFact()
        {
            if (!TryLive(out var current) || (uint)selectedFactSlot >= (uint)current.Facts.Catalog.Count)
            { RefreshNow(); return; }
            EditFact(current.Facts, selectedFactSlot, factValue.value);
            factValueDirty = false; RefreshNow();
        }
        void SetDebugMenus(bool enabled)
        {
            if (!TryLive(out var current)) { RefreshNow(); return; }
            int slot = current.Facts.Catalog.SlotOf("debug_menus");
            if (slot >= 0) EditFact(current.Facts, slot, enabled ? 1 : 0);
            RefreshNow();
        }
        public static void EditFact(FactStore store, int slot, int value)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if ((uint)slot >= (uint)store.Catalog.Count) throw new ArgumentOutOfRangeException(nameof(slot));
            store.SetBySlot(slot, value, FactWriteSource.Debugger);
        }
        void RefreshBaselines(FactStore store, bool questsAvailable)
        {
            if (!Game.Core.Services.TryGet<QuestService>(out var quests))
            { baselineStatus.text = "QuestService baseline reads are unavailable (QST-003)."; return; }
            if (!questsAvailable)
            { baselineStatus.text = "Active quest instances are unavailable (QST-006)."; return; }
            if (!Game.Core.Services.TryGet<ContentService>(out var content))
            { baselineStatus.text = "ContentService is unavailable; quest definitions cannot be resolved."; return; }
            for (int i = 0; i < activeQuests.Count; i++)
            {
                var quest = activeQuests[i];
                if (quest.Status != QuestStatus.Active) continue;
                var definition = quest.QuestIndex > 0 && quest.QuestIndex <= ushort.MaxValue ?
                    content.Catalog.GetByIndex<QuestDefinition>((ushort)quest.QuestIndex) : null;
                if (definition == null) { baselineRows.Add(quest.QuestId + " · quest definition unavailable"); continue; }
                baselineDependencies.Clear(); baselineSlots.Clear(); baselineSet.Clear();
                definition.FailWhen?.CollectDependencies(baselineDependencies);
                foreach (var stage in definition.Stages)
                    foreach (var objective in stage.objectives)
                    { objective.doneWhen?.CollectDependencies(baselineDependencies); objective.progress?.CollectDependencies(baselineDependencies); }
                bool bound = true;
                foreach (var dependency in baselineDependencies)
                {
                    if (dependency.Kind != LogicDependencyKind.Fact) continue;
                    if ((uint)dependency.Key >= (uint)store.Catalog.Count) { bound = false; break; }
                    if (baselineSet.Add(dependency.Key)) baselineSlots.Add(dependency.Key);
                }
                if (!bound) { baselineRows.Add(quest.QuestId + " · fact dependencies are not bound"); continue; }
                baselineSlots.Sort();
                int instance = quests.GetInstanceCount(quest.QuestIndex) - 1; // Latest instance; LogicScope uses a zero-based index.
                if (instance < 0) { baselineRows.Add(quest.QuestId + " · active instance unavailable"); continue; }
                var scope = new LogicScope(LogicUsage.QuestObjective, quest.QuestIndex, instance, 0, false, default);
                if (baselineSlots.Count == 0) baselineRows.Add(quest.QuestId + " · no fact dependencies");
                foreach (int slot in baselineSlots)
                {
                    int accept = quests.GetBaseline(scope, slot, BaselineKind.Accept), stage = quests.GetBaseline(scope, slot, BaselineKind.Stage);
                    baselineRows.Add(string.Format(CultureInfo.InvariantCulture, "{0} · instance {1} · {2}\nAccept: {3} · Stage: {4} · Current: {5}",
                        quest.QuestId, instance, store.Catalog.IdAt(slot).Name, accept, stage, store.GetBySlot(slot)));
                }
            }
            baselineStatus.text = activeQuests.Count == 0 ? "No active quests." : "Stored accept/stage baselines · instance indices are zero-based · read only";
        }
        void RefreshBeats(NarrativeService current)
        {
            string selected = beatChoice.value;
            beatIds.Clear();
            if (current != null && Game.Core.Services.TryGet<ContentService>(out var content))
                foreach (var beat in content.Catalog.All<StoryBeatDefinition>()) beatIds.Add(beat.IdString);
            beatIds.Sort(StringComparer.Ordinal); beatChoice.choices = beatIds;
            beatChoice.SetValueWithoutNotify(beatIds.Contains(selected) ? selected : beatIds.Count > 0 ? beatIds[0] : "");
            beatChoice.SetEnabled(beatIds.Count > 0);
            bool ready = current != null && current.IsWorldReady && beatIds.Count > 0;
            if (!Game.Core.Services.TryGet<BeatService>(out var beats))
            { ready = false; playStatus.text = "Play beat unavailable: BeatService is not registered (QST-005)."; }
            else if (!Game.Core.Services.TryGet<DialogService>(out _))
            { ready = false; playStatus.text = "Play beat unavailable: DialogService is not registered (DLG-006)."; }
            else playStatus.text = ready ? playMessage ?? "Play beat calls the live BeatService.DebugPlay; condition and repeat checks are bypassed." : "Play beat requires a ready world and a real beat definition.";
            playBeat.SetEnabled(ready);
        }
        void PlaySelectedBeat()
        {
            if (!TryLive(out var current) || !current.IsWorldReady || !Game.Core.Services.TryGet<ContentService>(out var content) ||
                !Game.Core.Services.TryGet<BeatService>(out var beats) || !Game.Core.Services.TryGet<DialogService>(out _))
            { RefreshNow(); return; }
            ushort index = 0;
            foreach (var beat in content.Catalog.All<StoryBeatDefinition>())
                if (beat.IdString == beatChoice.value) { index = content.Catalog.IndexOf(beat); break; }
            if (index == 0) { RefreshNow(); return; }
            beats.DebugPlay(index); playMessage = "DebugPlay requested: " + beatChoice.value;
            RefreshNow();
        }
        public static string FormatTraceEntry(LogicTraceEntry entry, string subjectName)
            => string.Format(CultureInfo.InvariantCulture, "{0} | {1:F3}s | {2} | {3} | {4} → {5} | {6} | ctx={7:X16}",
                entry.Frame, entry.UniversalTime, entry.Kind, subjectName, entry.OldValue, entry.NewValue, entry.Source, entry.ContextId);
    }
}
