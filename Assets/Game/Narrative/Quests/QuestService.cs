namespace Game.Narrative.Quests
{
    /// <summary>SPEC-42 §5.3 quest service contract; runtime behavior follows in QST-003.</summary>
    public sealed class QuestService : Game.Narrative.INarrativeSystem
    {
        public QuestService(Game.Data.Services.ContentCatalog catalog, FactStore facts, Game.Data.Logic.ILogicContext context, NarrativeService host) => throw new System.NotImplementedException("QST-003");
        public int Order => 10;
        public Game.Data.Logic.QuestStatus GetStatus(int questIndex) => throw new System.NotImplementedException("QST-003");            // latest instance; NotStarted if none
        public int GetStage(int questIndex) => throw new System.NotImplementedException("QST-003");                                     // -1 unless Active
        public int GetInstanceCount(int questIndex) => throw new System.NotImplementedException("QST-003");
        public bool IsObjectiveDone(int questIndex, string objectiveId) => throw new System.NotImplementedException("QST-003");         // latest instance, current or earlier stage
        public int GetBaseline(in Game.Data.Logic.LogicScope scope, int factSlot, Game.Data.Logic.BaselineKind kind) => throw new System.NotImplementedException("QST-003");
        public bool Start(int questIndex) => throw new System.NotImplementedException("QST-003");
        public bool Fail(int questIndex) => throw new System.NotImplementedException("QST-003");
        public bool ForceComplete(int questIndex) => throw new System.NotImplementedException("QST-003");
        public void Track(int questIndex) => throw new System.NotImplementedException("QST-003");                                        // 0 = none
        public int TrackedQuestIndex => throw new System.NotImplementedException("QST-003");
        public void AddLog(int questIndex, string entryId) => throw new System.NotImplementedException("QST-003");
        public void GetObjectives(int questIndex, System.Collections.Generic.List<QuestObjectiveView> into) => throw new System.NotImplementedException("QST-003");   // current stage
        public event System.Action<int> QuestChanged
        {
            add => throw new System.NotImplementedException("QST-003");
            remove => throw new System.NotImplementedException("QST-003");
        }                            // quest index (status, stage, objective, log, tracking)
        // INarrativeSystem members (§5.3)
        // TODO(QST-003): implement the canonical narrative lifecycle and owned runtime behavior.
        public void OnNewGame() => throw new System.NotImplementedException("QST-003");
        public void OnRestored() => throw new System.NotImplementedException("QST-003");
        public void Evaluate(System.Collections.Generic.IReadOnlyList<Game.Data.Logic.LogicDependency> changed, bool fullReevaluate)
            => throw new System.NotImplementedException("QST-003");
        public void AfterEvaluation(bool playerFree) => throw new System.NotImplementedException("QST-003");
    }
}
