namespace Game.Narrative.Quests
{
    /// <summary>SPEC-42 §4.5 instance and baseline DTO; QST-003 owns lifecycle.</summary>
    internal sealed class QuestInstance
    {
        public int QuestIndex = default; public int Instance = default; public Game.Data.Logic.QuestStatus Status = default; public int Stage = default;
        public ulong DoneBits = default;                                   // bit i = objective i of the current stage done (latched); 16 used
        public int[] AcceptBaselineSlots = default, AcceptBaselineValues = default;  // runtime slots; restored unknown names resolve to -1
        public int[] StageBaselineSlots = default, StageBaselineValues = default;    // runtime slots; restored unknown names resolve to -1; recaptured on stage entry
        public string[] AcceptBaselineNames, StageBaselineNames; // QUST retains unresolved names alongside resolved slots.
        public readonly System.Collections.Generic.List<string> Log = new();   // entry IDs in order
        public int AcceptOrder = default;                                  // monotonic counter value at accept (UI ordering)
    }
}
