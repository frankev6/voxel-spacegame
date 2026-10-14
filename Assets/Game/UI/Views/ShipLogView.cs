using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 HUD-014: three tabs, virtualized recycled rows and a fixed 16-objective pool.</summary>
    public sealed class ShipLogView : UIScreen
    {
        readonly ShipLogViewModel viewModel;
        readonly IShipLogCommands commands;
        readonly List<ListView> lists = new();
        readonly List<Row> rows = new();
        readonly Label[] objectives = new Label[16];
        readonly int[] selected = new int[1];
        ListView quests, clues;
        TabBar tabs;
        Toggle completed;
        VisualElement questPane, cluePane, rumorPane;
        Label title, stage, description, clueBody, questEmpty, clueEmpty, rumorEmpty;
        bool observing, refreshing;
        public ShipLogView(ShipLogViewModel vm, IShipLogCommands commands)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands)); }
        public override string Key => "ship-log";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => tabs;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            tabs = Q<TabBar>("ship-log-tabs"); tabs.focusable = true; tabs.SetTabs(new[] { "Quests", "Clues", "Rumors" }, new[] { "ship-log-tab-quests", "ship-log-tab-clues", "ship-log-tab-rumors" });
            completed = Q<Toggle>("ship-log-show-completed");
            questPane = Q<VisualElement>("ship-log-quests"); cluePane = Q<VisualElement>("ship-log-clues"); rumorPane = Q<VisualElement>("ship-log-rumors");
            title = Q<Label>("ship-log-quest-title"); stage = Q<Label>("ship-log-quest-stage"); description = Q<Label>("ship-log-quest-description"); clueBody = Q<Label>("ship-log-clue-body");
            questEmpty = Q<Label>("ship-log-quest-empty"); clueEmpty = Q<Label>("ship-log-clue-empty"); rumorEmpty = Q<Label>("ship-log-rumor-empty");
            quests = List("ship-log-quest-list", viewModel.Quests, "quest", i => viewModel.Quests[i]);
            clues = List("ship-log-clue-list", viewModel.Clues, "clue", i => viewModel.Clues[i]);
            List("ship-log-rumor-list", viewModel.Rumors, "rumor", i => viewModel.Rumors[i]);
            List("ship-log-mysteries", viewModel.Mysteries, "mystery", i => viewModel.Mysteries[i]);
            List("ship-log-journal", viewModel.Journal, "journal", i => viewModel.Journal[i]);
            List("ship-log-quest-log", viewModel.QuestLog, "log", i => viewModel.QuestLog[i]);
            var container = Q<VisualElement>("ship-log-objectives");
            for (int i = 0; i < objectives.Length; i++)
            { var label = new Label { name = "ship-log-objective-" + i, pickingMode = PickingMode.Ignore }; label.AddToClassList("ship-log-objective"); container.Add(label); objectives[i] = label; }
            tabs.SelectionChanged += tab => { if (observing && !refreshing) commands.SelectTab(tab); };
            completed.RegisterValueChangedCallback(evt => { if (observing && !refreshing) commands.SetShowCompleted(evt.newValue); });
            quests.selectionChanged += _ => { if (observing && !refreshing) commands.SelectQuest(quests.selectedIndex); };
            clues.selectionChanged += _ => { if (observing && !refreshing) commands.SelectClue(clues.selectedIndex); };
            Q<Button>("ship-log-close").clicked += () => { if (observing) Close(); };
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root)) Stop(); });
            Root.RegisterCallback<AttachToPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root) && IsOpen) Start(); });
        }
        ListView List(string name, IList source, string kind, Func<int, object> modelAt)
        {
            var list = Q<ListView>(name); var template = list.itemTemplate; list.itemTemplate = null;
            if ((kind == "quest" || kind == "clue" || kind == "rumor") && template == null) throw new InvalidOperationException("Ship log row template missing: " + kind);
            list.makeItem = () => { var row = new Row(this, kind, template?.Instantiate()); rows.Add(row); return row.Root; };
            list.bindItem = (element, index) => ((Row)element.userData).Bind(modelAt(index), index, observing,
                kind != "clue" || index == 0 || viewModel.Clues[index - 1].MysteryTitle != viewModel.Clues[index].MysteryTitle);
            list.unbindItem = (element, _) => ((Row)element.userData).Unbind();
            list.destroyItem = element => { var row = (Row)element.userData; row.Unbind(); rows.Remove(row); };
            list.itemsSource = source; lists.Add(list); return list;
        }
        public override void OnPush(object args) => Start();
        public override void OnPop() => Stop();
        void Start()
        {
            if (observing) return;
            commands.SelectTab(viewModel.SelectedTab); observing = true; viewModel.propertyChanged += Changed; Refresh();
        }
        void Stop()
        { if (!observing) return; observing = false; viewModel.propertyChanged -= Changed; foreach (var row in rows) row.Unbind(); }
        void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        static void Visible(VisualElement element, bool visible) => element.EnableInClassList(UIStyleClasses.IsHidden, !visible);
        void Selection(ListView list, int index)
        { selected[0] = index; list.SetSelectionWithoutNotify(index >= 0 ? selected : Array.Empty<int>()); }
        void Refresh()
        {
            if (!observing || refreshing) return; refreshing = true;
            try
            {
                tabs.SelectedIndex = viewModel.SelectedTab; completed.SetValueWithoutNotify(viewModel.ShowCompleted);
                Visible(questPane, viewModel.SelectedTab == 0); Visible(cluePane, viewModel.SelectedTab == 1); Visible(rumorPane, viewModel.SelectedTab == 2);
                title.text = viewModel.QuestTitle ?? ""; stage.text = viewModel.QuestStageText ?? ""; description.text = viewModel.QuestDescription ?? ""; clueBody.text = viewModel.ClueBody ?? "";
                Visible(questEmpty, viewModel.Quests.Count == 0); Visible(clueEmpty, viewModel.Clues.Count == 0); Visible(rumorEmpty, viewModel.Rumors.Count == 0);
                for (int i = 0; i < objectives.Length; i++)
                {
                    bool shown = i < viewModel.Objectives.Count; Visible(objectives[i], shown); if (!shown) continue;
                    var value = viewModel.Objectives[i]; objectives[i].text = (value.IsDone ? "[x] " : "[ ] ") + value.Text;
                    objectives[i].EnableInClassList("is-done", value.IsDone);
                }
                Selection(quests, viewModel.SelectedQuest); Selection(clues, viewModel.SelectedClue);
                foreach (var list in lists) list.RefreshItems();
            }
            finally { refreshing = false; }
        }
        sealed class Row
        {
            readonly ShipLogView owner;
            readonly string kind;
            internal readonly VisualElement Root;
            readonly Label title, subtitle, state, body;
            readonly Button action;
            readonly VisualElement background;
            object model;
            int index;
            bool active, group;
            internal Row(ShipLogView owner, string kind, VisualElement template)
            {
                this.owner = owner; this.kind = kind; Root = template ?? new VisualElement(); Root.userData = this;
                Root.pickingMode = kind == "clue" ? PickingMode.Position : PickingMode.Ignore;
                if (kind == "clue") Root.RegisterCallback<ClickEvent>(_ => { if (active && owner.observing) owner.commands.SelectClue(index); });
                if (template == null)
                {
                    Root.AddToClassList("list__item"); Root.AddToClassList("ship-log-" + kind + "-row");
                    title = new Label(); title.AddToClassList("text-m"); subtitle = new Label(); subtitle.AddToClassList("text-s");
                    body = new Label(); body.AddToClassList("text-s"); body.AddToClassList("ship-log-prose");
                    Root.Add(title); Root.Add(subtitle); Root.Add(body);
                }
                else { title = Root.Q<Label>("row-title"); subtitle = Root.Q<Label>("row-subtitle"); state = Root.Q<Label>("row-state"); action = Root.Q<Button>("row-action"); }
                background = Root.Q<VisualElement>(className: "list__item") ?? Root;
                if (action != null) action.clicked += () =>
                { if (!active || !owner.observing) return; if (kind == "quest") owner.commands.ToggleTrack(index); else owner.commands.ShowRumorOnMap(index); };
            }
            internal void Bind(object value, int rowIndex, bool observe, bool showGroup)
            {
                Unbind(); model = value; index = rowIndex; active = observe; group = showGroup; Root.dataSource = value is ViewModelBase ? value : null;
                Root.name = "ship-log-" + kind + "-row-" + index;
                if (action != null) action.name = kind == "quest" ? "ship-log-quest-track-" + index : "ship-log-rumor-map-" + index;
                if (active && model is ViewModelBase viewModel) viewModel.propertyChanged += Changed;
                Render();
            }
            internal void Unbind()
            { if (active && model is ViewModelBase viewModel) viewModel.propertyChanged -= Changed; active = false; model = null; Root.dataSource = null; }
            void Changed(object sender, BindablePropertyChangedEventArgs args) => Render();
            void Render()
            {
                if (model is QuestRowViewModel quest)
                {
                    title.text = quest.Title; title.tooltip = quest.Title; subtitle.text = quest.StageText;
                    action.text = quest.IsTracked ? "Tracked" : "Track"; action.SetEnabled(!quest.IsCompleted);
                    background.EnableInClassList(UIStyleClasses.IsSelected, quest.IsSelected); Visible(state, false);
                }
                else if (model is ClueRowViewModel clue)
                {
                    title.text = clue.Title; title.tooltip = clue.Title; subtitle.text = clue.MysteryTitle; Visible(subtitle, group);
                    state.text = "New"; Visible(state, clue.IsNew); background.EnableInClassList(UIStyleClasses.IsSelected, clue.IsSelected);
                }
                else if (model is RumorRowViewModel rumor)
                { title.text = rumor.Title; title.tooltip = rumor.Title; subtitle.text = rumor.LocationText; action.SetEnabled(rumor.HasMapTarget); }
                else if (model is MysteryRowViewModel mystery)
                {
                    title.text = mystery.Title; subtitle.text = mystery.Solved ? "Solved" : NumberStringCache.Get(mystery.Known) + "/" + NumberStringCache.Get(mystery.Required) + " clues";
                    body.text = mystery.SolvedText; Visible(body, !string.IsNullOrEmpty(body.text));
                }
                else
                { Visible(title, false); Visible(subtitle, false); body.text = model is JournalRowViewModel journal ? journal.Text : model as string ?? ""; }
            }
        }
    }
}
