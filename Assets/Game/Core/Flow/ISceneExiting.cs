namespace Game.Core.Flow
{
    /// <summary>Called by SceneFlow before a scene is torn down. Synchronous. Implements SPEC-02 §4.8.</summary>
    public interface ISceneExiting
    {
        int SceneExitOrder { get; }                                      // ascending
        void OnSceneExiting(in SceneExitContext context);
    }
}
