namespace Game.App.Boot
{
    /// <summary>Serialized on the GameRoot prefab. Implements SPEC-01 §4.2. UiConfig is added by SPEC-60.</summary>
    [System.Serializable]
    public sealed class GameRootConfig
    {
        public Game.Presentation.Data.CameraConfig CameraConfig;        // SPEC-62, CAM-004
        public Game.Data.ContentDatabase ContentDatabase;               // Assets/Game/Content/ContentDatabase.asset
        public UnityEngine.UIElements.UIDocument UiDocument;            // child "UI" of the prefab, with PanelSettings
    }
}
