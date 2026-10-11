namespace Game.Core.Settings
{
    /// <summary>Accessibility settings (SPEC-02 §4.9).</summary>
    public sealed class AccessibilitySettingsData
    {
        public float UiScale = 1f;                   // [0.8, 1.5] (SPEC-60 UIConfig range)
        public byte TextScaleIndex = 0;              // 0 = 100 %, 1 = 115 %, 2 = 130 % (SPEC-60 TextScale)
        public bool ReduceMotion = false;
        public float ScreenShakeScale = 1f;          // [0, 1]
        public void CopyFrom(AccessibilitySettingsData o)
        {
            UiScale = o.UiScale; TextScaleIndex = o.TextScaleIndex; ReduceMotion = o.ReduceMotion; ScreenShakeScale = o.ScreenShakeScale;
        }
        public void Clamp()
        {
            var d = new AccessibilitySettingsData();
            UiScale = SettingsClamp.Range(UiScale, 0.8f, 1.5f, d.UiScale);
            if (TextScaleIndex > 2) TextScaleIndex = 2;
            ScreenShakeScale = SettingsClamp.Range(ScreenShakeScale, 0f, 1f, d.ScreenShakeScale);
        }
        public bool ValueEquals(AccessibilitySettingsData o)
        {
            return UiScale == o.UiScale && TextScaleIndex == o.TextScaleIndex && ReduceMotion == o.ReduceMotion && ScreenShakeScale == o.ScreenShakeScale;
        }
    }
}
