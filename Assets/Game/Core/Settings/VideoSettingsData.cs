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
        public void CopyFrom(VideoSettingsData o) { throw new System.NotImplementedException("CORE-011"); }
        public void Clamp() { throw new System.NotImplementedException("CORE-011"); }
        public bool ValueEquals(VideoSettingsData o) { throw new System.NotImplementedException("CORE-011"); }
    }
}
