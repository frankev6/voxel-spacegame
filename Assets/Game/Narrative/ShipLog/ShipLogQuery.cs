namespace Game.Narrative.ShipLog
{
    /// <summary>SPEC-42 §5.5 ship-log read-model contract; behavior follows in QST-006.</summary>
    public sealed class ShipLogQuery
    {
        public uint Revision => throw new System.NotImplementedException("QST-006");                                    // +1 when anything listed changed
        public event System.Action Changed
        {
            add => throw new System.NotImplementedException("QST-006");
            remove => throw new System.NotImplementedException("QST-006");
        }
        public void GetQuests(bool completed, System.Collections.Generic.List<QuestEntryView> into) => throw new System.NotImplementedException("QST-006");   // Active (or Completed/Failed when completed == true), ordered by accept order
        public void GetObjectives(int questIndex, System.Collections.Generic.List<Game.Narrative.Quests.QuestObjectiveView> into) => throw new System.NotImplementedException("QST-006");
        public void GetLog(int questIndex, System.Collections.Generic.List<string> into) => throw new System.NotImplementedException("QST-006");              // texts of the latest instance's log
        public bool TryGetTracked(out QuestEntryView quest, out Game.Narrative.Quests.QuestObjectiveView currentObjective) => throw new System.NotImplementedException("QST-006");
        public void SetTracked(int questIndex) => throw new System.NotImplementedException("QST-006");
        public void GetClues(bool rumors, System.Collections.Generic.List<ClueEntryView> into) => throw new System.NotImplementedException("QST-006");        // known only, by ordinal
        public void GetMysteries(System.Collections.Generic.List<(string title, int known, int required, bool solved, string solvedText)> into) => throw new System.NotImplementedException("QST-006");
        public void GetJournal(System.Collections.Generic.List<JournalEntryView> into) => throw new System.NotImplementedException("QST-006");                // Journal facts with value ≠ 0, by name
        public System.Collections.Generic.IReadOnlyList<NarrativeMarker> Markers => throw new System.NotImplementedException("QST-006");              // rebuilt in AfterEvaluation when dirty
        public void MarkClueSeen(int clueIndex) => throw new System.NotImplementedException("QST-006");
    }
}
