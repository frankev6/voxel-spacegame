namespace Game.Core.Settings
{
    /// <summary>Player settings (SPEC-02 §4.9, §5.11). Persistence and application: SPEC-01 SettingsService.</summary>
    public sealed class GameSettings
    {
        public const int CurrentVersion = 1;
        public readonly AudioSettingsData Audio = new AudioSettingsData();
        public readonly VideoSettingsData Video = new VideoSettingsData();
        public readonly ControlSettingsData Controls = new ControlSettingsData();
        public readonly GameplaySettingsData Gameplay = new GameplaySettingsData();
        public readonly AccessibilitySettingsData Accessibility = new AccessibilitySettingsData();
        public void CopyFrom(GameSettings source, SettingsSection sections)
        {
            if ((sections & SettingsSection.Audio) != 0) Audio.CopyFrom(source.Audio);
            if ((sections & SettingsSection.Video) != 0) Video.CopyFrom(source.Video);
            if ((sections & SettingsSection.Controls) != 0) Controls.CopyFrom(source.Controls);
            if ((sections & SettingsSection.Gameplay) != 0) Gameplay.CopyFrom(source.Gameplay);
            if ((sections & SettingsSection.Accessibility) != 0) Accessibility.CopyFrom(source.Accessibility);
        }
        public GameSettings Clone()
        {
            var c = new GameSettings();
            c.CopyFrom(this, SettingsSection.All);
            return c;
        }                                     // deep copy of all sections
        public void ResetToDefaults(SettingsSection sections)
        {
            CopyFrom(new GameSettings(), sections);
        }
        public void Clamp()
        {
            Audio.Clamp(); Video.Clamp(); Controls.Clamp(); Gameplay.Clamp(); Accessibility.Clamp();
        }                                             // applies every section's Clamp()
        public SettingsSection DiffersFrom(GameSettings other)
        {
            var s = SettingsSection.None;
            if (!Audio.ValueEquals(other.Audio)) s |= SettingsSection.Audio;
            if (!Video.ValueEquals(other.Video)) s |= SettingsSection.Video;
            if (!Controls.ValueEquals(other.Controls)) s |= SettingsSection.Controls;
            if (!Gameplay.ValueEquals(other.Gameplay)) s |= SettingsSection.Gameplay;
            if (!Accessibility.ValueEquals(other.Accessibility)) s |= SettingsSection.Accessibility;
            return s;
        }          // sections whose values differ
    }
}
