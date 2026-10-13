namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    public enum QuestStatus : byte { NotStarted = 0, Active = 1, Completed = 2, Failed = 3, Abandoned = 4 }
}
