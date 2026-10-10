namespace Game.Core.Events
{
    /// <summary>Why the session was reset in place (SPEC-02 §4.5).</summary>
    public enum SessionResetReason : byte { NewGame = 0, Loaded = 1, Cleared = 2 }
}
