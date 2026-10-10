namespace Game.Core.Settings
{
    /// <summary>Audio settings; defaults equal SPEC-64 AudioVolumes.Default (SPEC-02 §4.9).</summary>
    public sealed class AudioSettingsData
    {
        public float MasterVolume = 0.8f;   // [0, 1]
        public float MusicVolume = 0.6f;    // [0, 1]
        public float SfxVolume = 0.8f;      // [0, 1]
        public float UiVolume = 0.7f;       // [0, 1]
        public float AmbienceVolume = 0.6f; // [0, 1]
        public bool MuteWhenUnfocused = true;
        public void CopyFrom(AudioSettingsData o) { throw new System.NotImplementedException("CORE-011"); }
        public void Clamp() { throw new System.NotImplementedException("CORE-011"); }
        public bool ValueEquals(AudioSettingsData o) { throw new System.NotImplementedException("CORE-011"); }
    }
}
