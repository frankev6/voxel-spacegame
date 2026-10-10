namespace Game.Core.Settings
{
    /// <summary>Accessibility settings (SPEC-02 §4.9).</summary>
    public sealed class AccessibilitySettingsData
    {
        public float UiScale = 1f;                   // [0.8, 1.5] (SPEC-60 UIConfig range)
        public byte TextScaleIndex = 0;              // 0 = 100 %, 1 = 115 %, 2 = 130 % (SPEC-60 TextScale)
        public bool ReduceMotion = false;
        public float ScreenShakeScale = 1f;          // [0, 1]
        public void CopyFrom(AccessibilitySettingsData o) { throw new System.NotImplementedException("CORE-011"); }
        public void Clamp() { throw new System.NotImplementedException("CORE-011"); }
        public bool ValueEquals(AccessibilitySettingsData o) { throw new System.NotImplementedException("CORE-011"); }
    }
}
