using Game.Data.Logic;

namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §5.5: logic calls route to the bound real quest, clue, beat and dialog owners.</summary>
    public sealed class NarrativeLogicAdapter : Game.Data.Logic.ILogicNarrative
    {
        Game.Narrative.Dialog.DialogService dialog;
        Game.Narrative.Quests.QuestService quests;
        Game.Narrative.Clues.ClueService clues;
        Game.Narrative.Beats.BeatService beats;
        public void BindServices(Game.Narrative.Quests.QuestService questService, Game.Narrative.Clues.ClueService clueService,
            Game.Narrative.Beats.BeatService beatService)
        {
            if (questService == null) throw new System.ArgumentNullException(nameof(questService));
            if (clueService == null) throw new System.ArgumentNullException(nameof(clueService));
            if (beatService == null) throw new System.ArgumentNullException(nameof(beatService));
            quests = questService; clues = clueService; beats = beatService;
        }
        Game.Narrative.Quests.QuestService Quests => quests ?? throw new System.InvalidOperationException("Quest service is not bound.");
        Game.Narrative.Clues.ClueService Clues => clues ?? throw new System.InvalidOperationException("Clue service is not bound.");
        Game.Narrative.Beats.BeatService Beats => beats ?? throw new System.InvalidOperationException("Beat service is not bound.");
        public void BindDialog(Game.Narrative.Dialog.DialogService value)
        { dialog = value ?? throw new System.ArgumentNullException(nameof(value)); }
        public QuestStatus GetQuestStatus(int questIndex) => Quests.GetStatus(questIndex);
        public int GetQuestStage(int questIndex) => Quests.GetStage(questIndex);
        public bool IsObjectiveDone(int questIndex, string objectiveId) => Quests.IsObjectiveDone(questIndex, objectiveId);
        public int GetBaseline(in LogicScope scope, int factSlot, BaselineKind kind) => Quests.GetBaseline(scope, factSlot, kind);
        public int GetDialogVisitCount(string node) => (dialog ?? throw new System.InvalidOperationException("Dialog is not bound.")).Memory.GetVisitCount(node);
        public int Roll(int sides) => (dialog ?? throw new System.InvalidOperationException("Dialog is not bound.")).Roll(sides);
        public bool StartQuest(int questIndex) => Quests.Start(questIndex);
        public bool FailQuest(int questIndex) => Quests.Fail(questIndex);
        public bool CompleteQuest(int questIndex) => Quests.ForceComplete(questIndex);
        public void TrackQuest(int questIndex) => Quests.Track(questIndex);
        public void AddQuestLog(int questIndex, string entryId) => Quests.AddLog(questIndex, entryId);
        public bool RevealClue(int clueIndex) => Clues.Reveal(clueIndex);
        public void QueueBeat(int beatIndex) => Beats.Queue(beatIndex);
        public void CompleteBeat(int beatIndex) => Beats.CompleteBeat(beatIndex);
        public void AbortCurrentBeat() => Beats.AbortCurrent();
    }
}
