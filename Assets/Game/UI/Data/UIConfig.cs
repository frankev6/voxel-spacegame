namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    [UnityEngine.CreateAssetMenu(menuName = "Game/UI/UI Config")]
    public sealed class UIConfig : UnityEngine.ScriptableObject
    {
        public UnityEngine.UIElements.PanelSettings PanelSettings;   // Theme/GamePanelSettings.asset
        public UnityEngine.UIElements.VisualTreeAsset RootUxml;      // Views/Uxml/UIRoot.uxml
        public UnityEngine.UIElements.VisualTreeAsset ToastUxml;     // Views/Uxml/Toast.uxml
        public UnityEngine.UIElements.VisualTreeAsset TooltipUxml;   // Views/Uxml/Tooltip.uxml
        public ScreenAsset[] Screens = new ScreenAsset[0];
        public float TooltipDelaySeconds = 0.35f;
        public int MaxVisibleToasts = 4;
        public int MaxQueuedToasts = 32;
        public float ToastLifetimeSeconds = 4.0f;
        public float ToastCoalesceWindowSeconds = 2.5f;
        public int ToastPoolSize = 8;
        public float MinUiScale = 0.8f;
        public float MaxUiScale = 1.5f;
        public string[] SoundCueIds = new string[6];   // index = (int)UISound; AudioCueDefinition ids, e.g. "cue_ui_click" (SPEC-64 §6.4)
    }
}
