namespace Game.Narrative
{
    /// <summary>Public narrative contract (SPEC-40 §4.10); behavior follows in the named implementation task.</summary>
    public enum LogicTraceKind : byte { FactWrite = 0, EffectApplied = 1, EffectRejected = 2, ConditionTrue = 3, BeatSelected = 4, BeatCompleted = 5, BeatAborted = 6, QuestAccepted = 7, StageCompleted = 8, QuestCompleted = 9, QuestFailed = 10, ClueRevealed = 11, MysterySolved = 12, PassLimitHit = 13 }
}
