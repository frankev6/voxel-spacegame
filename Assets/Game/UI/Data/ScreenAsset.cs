namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    [System.Serializable]
    public struct ScreenAsset
    {
        public string Key;                 // equals UIScreen.Key, kebab-case, unique
        public UnityEngine.UIElements.VisualTreeAsset Uxml;
        public bool Prewarm;               // build at UIRoot construction instead of first push
    }
}
