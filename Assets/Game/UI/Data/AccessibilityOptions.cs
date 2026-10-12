namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public struct AccessibilityOptions
    {
        public float UiScale;         // clamped to [UIConfig.MinUiScale, MaxUiScale]; default 1.0
        public TextScale TextScale;   // default Percent100
        public bool ReduceMotion;     // default false
    }
}
