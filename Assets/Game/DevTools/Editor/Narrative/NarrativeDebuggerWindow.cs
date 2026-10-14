using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Narrative;
using Game.Narrative.ShipLog;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.DevTools.Editor.Narrative
{
    /// <summary>SPEC-40 §5.9 M3: read-only snapshots of the live narrative services.</summary>
    public sealed class NarrativeDebuggerWindow : EditorWindow
    {
        const string LayoutPath = "Assets/Game/DevTools/Editor/Narrative/NarrativeDebuggerWindow.uxml";
        const string ThemePath = "Assets/Game/UI/Theme/NarrativeDebugger.uss";
        const double RefreshSeconds = .5;
        readonly List<FactRow> facts = new();
        readonly List<string> traceRows = new(), questRows = new();
        readonly List<QuestEntryView> activeQuests = new(), finishedQuests = new();
        readonly LogicTraceEntry[] traceBuffer = new LogicTraceEntry[LogicTraceLog.Capacity];
        MultiColumnListView factsView;
        ListView traceView, questsView;
        ToolbarSearchField search;
        Label status, factStatus, traceStatus, questStatus;
        VisualElement factsPanel, tracePanel, questsPanel;
        ToolbarToggle factsTab, traceTab, questsTab;
        NarrativeService observed;
        ShipLogQuery unavailableQuery;
        int traceVersion;
        bool wasPlaying;
        double refreshAt;

        readonly struct FactRow
        {
            internal readonly string Name, Family, Kind, Value, Default;
            internal readonly bool NonDefault;
            internal FactRow(Game.Data.Logic.FactCatalog catalog, int slot, int value)
            {
                Name = catalog.IdAt(slot).Name; Family = catalog.FamilyAt(slot).ToString(); Kind = catalog.KindAt(slot).ToString();
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
            observed = null; unavailableQuery = null;
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
            factsTab.RegisterValueChangedCallback(_ => SelectTab(0)); traceTab.RegisterValueChangedCallback(_ => SelectTab(1));
            questsTab.RegisterValueChangedCallback(_ => SelectTab(2)); search.RegisterValueChangedCallback(_ => RefreshNow());
            rootVisualElement.Q<ToolbarButton>("refresh").clicked += RefreshNow;
            factsView = new MultiColumnListView { itemsSource = facts, selectionType = SelectionType.None };
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
            factsPanel.EnableInClassList("narrative-debugger-hidden", tab != 0);
            tracePanel.EnableInClassList("narrative-debugger-hidden", tab != 1);
            questsPanel.EnableInClassList("narrative-debugger-hidden", tab != 2);
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
            observed = current; wasPlaying = EditorApplication.isPlaying;
            traceVersion = current?.Facts.Trace?.Version ?? 0; refreshAt = EditorApplication.timeSinceStartup + RefreshSeconds;
            facts.Clear(); traceRows.Clear(); questRows.Clear();
            if (current == null)
            {
                status.text = wasPlaying ? "NarrativeService is not registered in this Play session." : "Enter Play mode to inspect the live narrative.";
                factStatus.text = traceStatus.text = questStatus.text = "Unavailable until a live narrative service is registered.";
            }
            else
            {
                status.text = current.IsWorldReady ? "Live narrative · read only" : "Narrative available · world not ready · read only";
                RefreshFacts(current.Facts);
                RefreshTrace(current.Facts);
                RefreshQuests();
            }
            factsView.RefreshItems(); traceView.RefreshItems(); questsView.RefreshItems();
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
        void RefreshQuests()
        {
            if (!Game.Core.Services.TryGet<ShipLogQuery>(out var query))
            { questStatus.text = "ShipLogQuery is not registered (QST-006)."; return; }
            if (ReferenceEquals(query, unavailableQuery))
            { questStatus.text = "Ship-log reads are not implemented yet (QST-006)."; return; }
            activeQuests.Clear(); finishedQuests.Clear();
            try
            {
                query.GetQuests(false, activeQuests); query.GetQuests(true, finishedQuests);
                AppendQuests(activeQuests); AppendQuests(finishedQuests);
                questStatus.text = string.Format(CultureInfo.InvariantCulture, "{0} active · {1} completed or failed · read only", activeQuests.Count, finishedQuests.Count);
            }
            catch (NotImplementedException)
            {
                unavailableQuery = query; activeQuests.Clear(); finishedQuests.Clear(); questRows.Clear();
                questStatus.text = "Ship-log reads are not implemented yet (QST-006).";
            }
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
        public static string FormatTraceEntry(LogicTraceEntry entry, string subjectName)
            => string.Format(CultureInfo.InvariantCulture, "{0} | {1:F3}s | {2} | {3} | {4} → {5} | {6} | ctx={7:X16}",
                entry.Frame, entry.UniversalTime, entry.Kind, subjectName, entry.OldValue, entry.NewValue, entry.Source, entry.ContextId);
    }
}
