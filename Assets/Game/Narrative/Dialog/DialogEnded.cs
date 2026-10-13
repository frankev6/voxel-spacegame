namespace Game.Narrative.Dialog
{
    /// <summary>Implements SPEC-41 §4.7.</summary>
    public readonly struct DialogEnded
    {
        public readonly int BeatIndex; public readonly DialogOutcome Outcome;
        public readonly Game.Data.Logic.IEffect PendingTerminal;   // null or the terminal effect to run after the screen pops
        public readonly Game.Data.Logic.LogicScope Scope;
        public DialogEnded(int beatIndex, DialogOutcome outcome, Game.Data.Logic.IEffect pendingTerminal, in Game.Data.Logic.LogicScope scope)
        { BeatIndex = beatIndex; Outcome = outcome; PendingTerminal = pendingTerminal; Scope = scope; }
    }
}
