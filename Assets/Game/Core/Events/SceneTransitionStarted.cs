namespace Game.Core.Events
{
    /// <summary>SceneFlow began a transition (SPEC-02 §4.5).</summary>
    public readonly struct SceneTransitionStarted
    {
        public readonly Game.Core.Flow.GameScene From; public readonly Game.Core.Flow.GameScene To; public readonly Game.Core.Flow.SceneEntryReason Reason;
        public SceneTransitionStarted(Game.Core.Flow.GameScene from, Game.Core.Flow.GameScene to, Game.Core.Flow.SceneEntryReason reason) { From = from; To = to; Reason = reason; }
    }
}
