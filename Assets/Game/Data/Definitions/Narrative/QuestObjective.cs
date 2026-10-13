namespace Game.Data.Definitions
{
    /// <summary>SPEC-42 §4.3 objective data and compiled logic fields.</summary>
    [System.Serializable] public sealed class QuestObjective
    {
        public string id;                                      // snake_case, unique in the quest
        public string text;                                    // shown in the log and the HUD
        public Game.Data.Logic.CompiledCondition doneWhen = new();   // QuestObjective; required
        public Game.Data.Logic.CompiledIntExpr progress = new();     // QuestObjective; empty = no progress display
        public int target;                                     // progress target (0 = none)
        public bool latch = true;                              // stays done once done
        public bool optional;                                  // not required for stage completion
        public QuestMarker marker = new();
    }
}
