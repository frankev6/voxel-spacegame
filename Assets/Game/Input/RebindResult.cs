namespace Game.Input
{
    /// <summary>Result passed to the StartRebind callback; ConflictActionPath is set for Conflict. Implements SPEC-03 §4.3.</summary>
    public readonly struct RebindResult
    {
        public readonly RebindStatus Status; public readonly string ActionPath; public readonly string NewDisplay; public readonly string ConflictActionPath;
        public RebindResult(RebindStatus s, string path, string display, string conflict) { Status = s; ActionPath = path; NewDisplay = display; ConflictActionPath = conflict; }
    }
}
