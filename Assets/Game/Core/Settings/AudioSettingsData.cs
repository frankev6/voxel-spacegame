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
        public void CopyFrom(AudioSettingsData o)
        {
            MasterVolume = o.MasterVolume; MusicVolume = o.MusicVolume; SfxVolume = o.SfxVolume; UiVolume = o.UiVolume;
            AmbienceVolume = o.AmbienceVolume; MuteWhenUnfocused = o.MuteWhenUnfocused;
        }
        public void Clamp()
        {
            var d = new AudioSettingsData();
            MasterVolume = SettingsClamp.Range(MasterVolume, 0f, 1f, d.MasterVolume);
            MusicVolume = SettingsClamp.Range(MusicVolume, 0f, 1f, d.MusicVolume);
            SfxVolume = SettingsClamp.Range(SfxVolume, 0f, 1f, d.SfxVolume);
            UiVolume = SettingsClamp.Range(UiVolume, 0f, 1f, d.UiVolume);
            AmbienceVolume = SettingsClamp.Range(AmbienceVolume, 0f, 1f, d.AmbienceVolume);
        }
        public bool ValueEquals(AudioSettingsData o)
        {
            return MasterVolume == o.MasterVolume && MusicVolume == o.MusicVolume && SfxVolume == o.SfxVolume && UiVolume == o.UiVolume
                && AmbienceVolume == o.AmbienceVolume && MuteWhenUnfocused == o.MuteWhenUnfocused;
        }
    }
}
