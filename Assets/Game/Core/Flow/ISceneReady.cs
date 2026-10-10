namespace Game.Core.Flow
{
    /// <summary>Called by SceneFlow after a scene is loaded and GameplayActive exists (World). Sequential by order (SPEC-01 §5.4).
    /// Implements SPEC-02 §4.8.</summary>
    public interface ISceneReady
    {
        int SceneReadyOrder { get; }                                     // ascending; see SceneHookOrder
        void OnSceneReady(in SceneReadyContext context);                 // called once per scene entry
        bool IsSceneReadyComplete(in SceneReadyContext context);         // polled once per frame until true (timeout: SPEC-01)
    }
}
