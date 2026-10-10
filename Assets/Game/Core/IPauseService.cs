namespace Game.Core
{
    /// <summary>Ref-counted pause (architecture §6.1). Implemented by SPEC-01 PauseService. Implements SPEC-02 §4.4.</summary>
    public interface IPauseService
    {
        PauseToken Acquire(string reason);                    // reason: non-empty, e.g. "UINavigator", "SceneFlow"
        bool IsPaused { get; }                                // ActiveTokenCount > 0
        int ActiveTokenCount { get; }
        void Release(PauseToken token);                       // idempotent per token; unknown token → no-op
        void GetActiveReasons(System.Collections.Generic.List<string> results);   // clears results first
    }
}
