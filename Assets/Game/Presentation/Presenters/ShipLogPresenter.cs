using System;
using System.Collections.Generic;
using Game.Core;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Narrative.Quests;
using Game.Narrative.ShipLog;
using Game.UI.Data;
using Game.UI.ViewModels;
using Game.UI.Views;

namespace Game.Presentation.Presenters
{
    /// <summary>SPEC-61 HUD-014: real ship-log queries projected into stable pooled rows.</summary>
    public sealed class ShipLogPresenter : IShipLogCommands, IDisposable
    {
        readonly ShipLogViewModel model;
        readonly ContentCatalog catalog;
        readonly Func<ContentId, bool> showOnMap;
        readonly Action<string> unavailable;
        readonly List<QuestEntryView> quests = new(64);
        readonly List<ClueEntryView> clues = new(256), rumors = new(64);
        readonly List<QuestObjectiveView> objectives = new(16);
        readonly List<JournalEntryView> journal = new(64);
        readonly List<(string title, int known, int required, bool solved, string solvedText)> mysteries = new(16);
        readonly List<QuestRowViewModel> questPool = new(64);
        readonly List<ClueRowViewModel> cluePool = new(256);
        readonly List<RumorRowViewModel> rumorPool = new(64);
        readonly List<ObjectiveRowViewModel> objectivePool = new(16);
        readonly List<MysteryRowViewModel> mysteryPool = new(16);
        readonly List<JournalRowViewModel> journalPool = new(64);
        ShipLogQuery query;
        int selectedQuest, selectedClue;
        bool disposed;
        public ShipLogPresenter(ShipLogViewModel model, ContentCatalog catalog, Func<ContentId, bool> showOnMap = null, Action<string> unavailable = null)
        {
            this.model = model ?? throw new ArgumentNullException(nameof(model)); this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.showOnMap = showOnMap; this.unavailable = unavailable;
        }
        public void Bind(ShipLogQuery source)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ShipLogPresenter));
            Unbind(); query = source ?? throw new ArgumentNullException(nameof(source)); query.Changed += Refresh; Refresh();
        }
        public void Unbind()
        {
            if (query != null) query.Changed -= Refresh;
            query = null; selectedQuest = selectedClue = 0; Refresh();
        }
        static T Row<T>(List<T> pool, int index) where T : new()
        { while (pool.Count <= index) pool.Add(new T()); return pool[index]; }
        void Refresh()
        {
            quests.Clear(); clues.Clear(); rumors.Clear(); mysteries.Clear(); journal.Clear();
            if (query != null)
            {
                query.GetQuests(model.ShowCompleted, quests); query.GetClues(false, clues); query.GetClues(true, rumors);
                query.GetMysteries(mysteries); query.GetJournal(journal);
                clues.Sort(CompareClues);
            }
            model.Quests.Clear(); model.Clues.Clear(); model.Rumors.Clear(); model.Mysteries.Clear(); model.Journal.Clear();
            int questSelection = -1, clueSelection = -1;
            for (int i = 0; i < quests.Count; i++)
            {
                var value = quests[i]; var row = Row(questPool, i);
                row.QuestId = value.QuestId; row.Title = value.Title; row.StageText = StageText(value);
                row.IsTracked = value.Tracked; row.IsCompleted = value.Status != Game.Data.Logic.QuestStatus.Active;
                if (value.QuestIndex == selectedQuest) questSelection = i;
                model.Quests.Add(row);
            }
            for (int i = 0; i < clues.Count; i++)
            {
                var value = clues[i]; var row = Row(cluePool, i);
                row.ClueId = value.ClueId; row.Title = value.Title;
                row.MysteryTitle = string.IsNullOrEmpty(value.MysteryTitle) ? "Other clues" : value.MysteryTitle; row.IsNew = value.IsNew;
                if (value.ClueIndex == selectedClue) clueSelection = i;
                model.Clues.Add(row);
            }
            for (int i = 0; i < rumors.Count; i++)
            {
                var value = rumors[i]; var row = Row(rumorPool, i);
                row.RumorId = value.ClueId; row.Title = value.Title;
                string location = catalog.TryGet<AuthoredStarSystemDefinition>(value.TargetStarSystem, out var system) ? system.DisplayName : value.TargetStarSystem.Value ?? "";
                row.LocationText = value.Resolved ? "Resolved" : location;
                row.HasMapTarget = !value.Resolved && !value.TargetStarSystem.IsNone; model.Rumors.Add(row);
            }
            for (int i = 0; i < mysteries.Count; i++)
            {
                var value = mysteries[i]; var row = Row(mysteryPool, i);
                row.Title = value.title; row.Known = value.known; row.Required = value.required; row.Solved = value.solved; row.SolvedText = value.solvedText;
                model.Mysteries.Add(row);
            }
            for (int i = 0; i < journal.Count; i++)
            { var value = journal[i]; var row = Row(journalPool, i); row.Text = value.Text; row.Value = value.Value; model.Journal.Add(row); }
            model.SelectedQuest = questSelection >= 0 ? questSelection : quests.Count == 0 ? -1 : 0;
            model.SelectedClue = clueSelection >= 0 ? clueSelection : clues.Count == 0 ? -1 : 0;
            selectedQuest = model.SelectedQuest < 0 ? 0 : quests[model.SelectedQuest].QuestIndex;
            selectedClue = model.SelectedClue < 0 ? 0 : clues[model.SelectedClue].ClueIndex;
            for (int i = 0; i < model.Quests.Count; i++) model.Quests[i].IsSelected = i == model.SelectedQuest;
            for (int i = 0; i < model.Clues.Count; i++) model.Clues[i].IsSelected = i == model.SelectedClue;
            QuestDetails(); ClueDetails();
            model.NotifyQuests(); model.NotifyClues(); model.NotifyRumors(); model.NotifyMysteries(); model.NotifyJournal();
        }
        static int CompareClues(ClueEntryView a, ClueEntryView b)
        { int group = string.CompareOrdinal(a.MysteryTitle, b.MysteryTitle); return group != 0 ? group : a.Ordinal.CompareTo(b.Ordinal); }
        static string StageText(QuestEntryView quest) => quest.Status == Game.Data.Logic.QuestStatus.Failed ? "Failed" :
            quest.Status == Game.Data.Logic.QuestStatus.Completed ? "Completed" : quest.StageText;
        void QuestDetails()
        {
            model.Objectives.Clear(); model.QuestLog.Clear(); model.QuestTitle = model.QuestStageText = model.QuestDescription = "";
            if (query != null && model.SelectedQuest >= 0 && model.SelectedQuest < quests.Count)
            {
                var value = quests[model.SelectedQuest]; model.QuestTitle = value.Title; model.QuestStageText = StageText(value);
                model.QuestDescription = catalog.TryGet<QuestDefinition>(new ContentId(value.QuestId), out var definition) && !string.IsNullOrEmpty(definition.Description) ? definition.Description : value.ShortDescription;
                query.GetObjectives(value.QuestIndex, objectives); query.GetLog(value.QuestIndex, model.QuestLog);
                for (int i = 0; i < objectives.Count; i++)
                {
                    var valueObjective = objectives[i]; var row = Row(objectivePool, i);
                    row.Text = valueObjective.Text ?? "";
                    if (valueObjective.Target > 0) row.Text += " (" + NumberStringCache.Get(valueObjective.Progress) + "/" + NumberStringCache.Get(valueObjective.Target) + ")";
                    if (valueObjective.Optional) row.Text += " (optional)";
                    row.IsDone = valueObjective.Done; model.Objectives.Add(row);
                }
            }
            model.NotifyObjectives(); model.NotifyQuestLog();
        }
        void ClueDetails()
            => model.ClueBody = query != null && model.SelectedClue >= 0 && model.SelectedClue < clues.Count ? clues[model.SelectedClue].Body : "";
        public void SelectTab(int tab)
        {
            if (disposed || tab < 0 || tab > 2) return;
            model.SelectedTab = tab; Refresh();
            if (tab == 1 && model.SelectedClue >= 0) SelectClue(model.SelectedClue);
        }
        public void SetShowCompleted(bool on) { if (disposed) return; model.ShowCompleted = on; Refresh(); }
        public void SelectQuest(int index)
        {
            if (query == null || index < 0 || index >= quests.Count) return;
            selectedQuest = quests[index].QuestIndex; model.SelectedQuest = index;
            for (int i = 0; i < model.Quests.Count; i++) model.Quests[i].IsSelected = i == index;
            QuestDetails();
        }
        public void ToggleTrack(int index)
        {
            if (query == null || index < 0 || index >= quests.Count || model.Quests[index].IsCompleted) return;
            query.SetTracked(quests[index].Tracked ? 0 : quests[index].QuestIndex); Refresh();
        }
        public void SelectClue(int index)
        {
            if (query == null || index < 0 || index >= clues.Count) return;
            selectedClue = clues[index].ClueIndex; model.SelectedClue = index;
            query.MarkClueSeen(selectedClue); Refresh();
        }
        public void ShowRumorOnMap(int index)
        {
            if (query == null || index < 0 || index >= rumors.Count || !model.Rumors[index].HasMapTarget) return;
            // TODO(HUD-016): App supplies real registered galaxy-map navigation with this authored system selected.
            if (showOnMap == null || !showOnMap(rumors[index].TargetStarSystem)) unavailable?.Invoke("Galaxy map is not available yet");
        }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; }
    }
}
