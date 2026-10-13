namespace Game.Data.Logic
{
    /// <summary>Public narrative contract (SPEC-40 §4.3); later task implementations are explicitly labelled.</summary>
    [System.Flags] public enum LogicUsage : ushort
    {
        None = 0, BeatCondition = 1, QuestObjective = 2, QuestEffect = 4, DialogCondition = 8, DialogCommand = 16,
        EncounterCondition = 32, SpawnCondition = 64, LaneCondition = 128, MarkerCondition = 256, BeatEffect = 512,
        AnyCondition = BeatCondition | QuestObjective | DialogCondition | EncounterCondition | SpawnCondition | LaneCondition | MarkerCondition,
        AnyEffect = QuestEffect | DialogCommand | BeatEffect
    }
}
