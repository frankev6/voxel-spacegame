namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public interface ILogicNarrative                   // implemented by Game.Narrative.NarrativeLogicAdapter (SPEC-42)
    {
        QuestStatus GetQuestStatus(int questIndex);     // latest instance
        int GetQuestStage(int questIndex);              // -1 unless Active
        bool IsObjectiveDone(int questIndex, string objectiveId);
        int GetBaseline(in LogicScope scope, int factSlot, BaselineKind kind);
        int GetDialogVisitCount(string node);           // SPEC-41 DialogMemory
        int Roll(int sides);                            // SPEC-41 §5.6, dialog only
        bool StartQuest(int questIndex);
        bool FailQuest(int questIndex);
        bool CompleteQuest(int questIndex);
        void TrackQuest(int questIndex);
        void AddQuestLog(int questIndex, string entryId);
        bool RevealClue(int clueIndex);
        void QueueBeat(int beatIndex);
        void CompleteBeat(int beatIndex);
        void AbortCurrentBeat();
    }
}
