namespace Game.Core.Flow
{
    /// <summary>Context passed to ISceneReady hooks (SPEC-02 §4.8).</summary>
    public readonly struct SceneReadyContext
    {
        public readonly GameScene Scene; public readonly SceneEntryReason Reason; public readonly BlueprintLabArgs LabArgs;
        public SceneReadyContext(GameScene scene, SceneEntryReason reason, BlueprintLabArgs labArgs) { Scene = scene; Reason = reason; LabArgs = labArgs; }
    }
}
