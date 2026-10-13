namespace Game.Narrative.Dialog
{
    /// <summary>One option of the current option set. Implements SPEC-41 §4.6.</summary>
    public struct DialogOption
    {
        public int Id;                       // pass to SelectOption
        public string LineId; public string Text;
        public bool Available;               // condition result (true when no condition)
        public bool HideWhenUnavailable;     // #hide
        public bool IsQuest;                 // #quest
        public bool IsLeave;                 // #leave
    }
}
