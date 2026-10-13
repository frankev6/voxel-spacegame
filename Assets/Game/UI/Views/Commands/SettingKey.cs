namespace Game.UI.Views
{
    /// <summary>Screen command contract (SPEC-61 §4.3).</summary>
    public enum SettingKey : byte
    {
        MasterVolume,
        MusicVolume,
        SfxVolume,
        UiVolume,
        Resolution,
        FullscreenMode,
        VSync,
        Quality,
        MouseSensitivity,
        InvertOrbitY,
        CameraYawMode,
        ScreenShake,
        UiScale,
        TextScale,
        ReduceMotion
    }
}
