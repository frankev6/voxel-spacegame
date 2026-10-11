namespace Game.Core.Settings
{
    /// <summary>Video settings (SPEC-02 §4.9).</summary>
    public sealed class VideoSettingsData
    {
        public int ResolutionWidth = 0;     // 0 = keep current; else [640, 7680]
        public int ResolutionHeight = 0;    // 0 = keep current; else [360, 4320] (both 0 or both set; one 0 → both 0)
        public int RefreshRateHz = 0;       // 0 = default; else [24, 500]
        public UnityEngine.FullScreenMode FullScreenMode = UnityEngine.FullScreenMode.FullScreenWindow;
        public bool VSync = true;
        public int TargetFrameRate = -1;    // -1 = platform default; else [30, 500]
        public int QualityLevel = -1;       // -1 = keep project default; else [0, 15] (SPEC-01 re-clamps to QualitySettings.names.Length - 1)
        public byte VfxQuality = 2;         // 0 Low, 1 Medium, 2 High (SPEC-63 FxQuality); [0, 2]
        public void CopyFrom(VideoSettingsData o)
        {
            ResolutionWidth = o.ResolutionWidth; ResolutionHeight = o.ResolutionHeight; RefreshRateHz = o.RefreshRateHz; FullScreenMode = o.FullScreenMode;
            VSync = o.VSync; TargetFrameRate = o.TargetFrameRate; QualityLevel = o.QualityLevel; VfxQuality = o.VfxQuality;
        }
        public void Clamp()
        {
            ResolutionWidth = SettingsClamp.ZeroOrRange(ResolutionWidth, 640, 7680);
            ResolutionHeight = SettingsClamp.ZeroOrRange(ResolutionHeight, 360, 4320);
            if (ResolutionWidth == 0 || ResolutionHeight == 0) { ResolutionWidth = 0; ResolutionHeight = 0; }
            RefreshRateHz = SettingsClamp.ZeroOrRange(RefreshRateHz, 24, 500);
            if (!System.Enum.IsDefined(typeof(UnityEngine.FullScreenMode), FullScreenMode)) FullScreenMode = new VideoSettingsData().FullScreenMode;
            TargetFrameRate = TargetFrameRate == -1 ? -1 : System.Math.Clamp(TargetFrameRate, 30, 500);
            QualityLevel = QualityLevel < 0 ? -1 : System.Math.Min(QualityLevel, 15);
            if (VfxQuality > 2) VfxQuality = 2;
        }
        public bool ValueEquals(VideoSettingsData o)
        {
            return ResolutionWidth == o.ResolutionWidth && ResolutionHeight == o.ResolutionHeight && RefreshRateHz == o.RefreshRateHz
                && FullScreenMode == o.FullScreenMode && VSync == o.VSync && TargetFrameRate == o.TargetFrameRate && QualityLevel == o.QualityLevel
                && VfxQuality == o.VfxQuality;
        }
    }
}
