namespace Game.Core.Flow
{
    /// <summary>Context passed to ISceneExiting hooks (SPEC-02 §4.8).</summary>
    public readonly struct SceneExitContext
    {
        public readonly GameScene Scene; public readonly GameScene NextScene; public readonly SceneEntryReason Reason;
        public SceneExitContext(GameScene scene, GameScene nextScene, SceneEntryReason reason) { Scene = scene; NextScene = nextScene; Reason = reason; }
    }
}
