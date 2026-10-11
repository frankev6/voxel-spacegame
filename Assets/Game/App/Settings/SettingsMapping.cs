using Game.Core.Audio;
using Game.Core.Settings;

namespace Game.App.Settings
{
    /// <summary>Pure conversions between settings sections and consumer types (SPEC-01 §5.2 rules).</summary>
    public static class SettingsMapping
    {
        public static AudioVolumes ToVolumes(AudioSettingsData a) => new AudioVolumes
        { Master = a.MasterVolume, Music = a.MusicVolume, Sfx = a.SfxVolume, Ui = a.UiVolume, Ambience = a.AmbienceVolume };
    }
}
