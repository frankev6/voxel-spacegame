namespace Game.Core.Events
{
    /// <summary>The pause state or token count changed (SPEC-02 §4.5).</summary>
    public readonly struct PauseStateChanged { public readonly bool IsPaused; public readonly int TokenCount; public PauseStateChanged(bool isPaused, int tokenCount) { IsPaused = isPaused; TokenCount = tokenCount; } }
}
