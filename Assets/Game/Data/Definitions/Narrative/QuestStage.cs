namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.3 objective stage and completion effects.</summary>
    [System.Serializable] public sealed class QuestStage
    {
        public string id;                                      // snake_case, unique in the quest
        public string title;                                   // "" = none
        public System.Collections.Generic.List<QuestObjective> objectives = new();   // 1..16
        public Game.Data.Logic.CompiledEffectList onComplete = new();               // QuestEffect
    }
}
