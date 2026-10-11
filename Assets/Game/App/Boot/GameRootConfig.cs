namespace Game.App.Boot
{
    /// <summary>Serialized on the GameRoot prefab. Implements SPEC-01 §4.2. UiConfig (SPEC-60) and CameraConfig (SPEC-62) are added by the
    /// tasks that create those types.</summary>
    [System.Serializable]
    public sealed class GameRootConfig
    {
        public Game.Data.ContentDatabase ContentDatabase;               // Assets/Game/Content/ContentDatabase.asset
        public UnityEngine.UIElements.UIDocument UiDocument;            // child "UI" of the prefab, with PanelSettings
    }
}
