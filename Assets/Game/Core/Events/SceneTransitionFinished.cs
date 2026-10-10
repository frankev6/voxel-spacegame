namespace Game.Core.Events
{
    /// <summary>SceneFlow finished a transition (SPEC-02 §4.5).</summary>
    public readonly struct SceneTransitionFinished
    {
        public readonly Game.Core.Flow.GameScene Scene; public readonly Game.Core.Flow.SceneEntryReason Reason; public readonly bool Success;
        public SceneTransitionFinished(Game.Core.Flow.GameScene scene, Game.Core.Flow.SceneEntryReason reason, bool success) { Scene = scene; Reason = reason; Success = success; }
    }
}
