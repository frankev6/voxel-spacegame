using Game.Data.Logic;

namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §5.5 canonical logic-to-narrative contract; routing follows in QST-006.</summary>
    public sealed class NarrativeLogicAdapter : Game.Data.Logic.ILogicNarrative
    {
        // TODO(QST-006): route each member through the canonical services and dialog.
        public QuestStatus GetQuestStatus(int questIndex) => throw new System.NotImplementedException("QST-006");
        public int GetQuestStage(int questIndex) => throw new System.NotImplementedException("QST-006");
        public bool IsObjectiveDone(int questIndex, string objectiveId) => throw new System.NotImplementedException("QST-006");
        public int GetBaseline(in LogicScope scope, int factSlot, BaselineKind kind) => throw new System.NotImplementedException("QST-006");
        public int GetDialogVisitCount(string node) => throw new System.NotImplementedException("QST-006");
        public int Roll(int sides) => throw new System.NotImplementedException("QST-006");
        public bool StartQuest(int questIndex) => throw new System.NotImplementedException("QST-006");
        public bool FailQuest(int questIndex) => throw new System.NotImplementedException("QST-006");
        public bool CompleteQuest(int questIndex) => throw new System.NotImplementedException("QST-006");
        public void TrackQuest(int questIndex) => throw new System.NotImplementedException("QST-006");
        public void AddQuestLog(int questIndex, string entryId) => throw new System.NotImplementedException("QST-006");
        public bool RevealClue(int clueIndex) => throw new System.NotImplementedException("QST-006");
        public void QueueBeat(int beatIndex) => throw new System.NotImplementedException("QST-006");
        public void CompleteBeat(int beatIndex) => throw new System.NotImplementedException("QST-006");
        public void AbortCurrentBeat() => throw new System.NotImplementedException("QST-006");
    }
}
