namespace Game.Core.Flow
{
    /// <summary>Order values for ISceneReady and ISceneExiting hooks (SPEC-02 §4.8).</summary>
    public static class SceneHookOrder
    {
        public const int Session = 50, Galaxy = 100, PlayerShip = 200, Camera = 300, Ui = 400, Narrative = 500, Audio = 600, Editor = 700, Default = 1000;
    }
}
