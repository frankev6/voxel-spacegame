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
        public void CopyFrom(GameSettings source, SettingsSection sections) { throw new System.NotImplementedException("CORE-011"); }
        public GameSettings Clone() { throw new System.NotImplementedException("CORE-011"); }                                     // deep copy of all sections
        public void ResetToDefaults(SettingsSection sections) { throw new System.NotImplementedException("CORE-011"); }
        public void Clamp() { throw new System.NotImplementedException("CORE-011"); }                                             // applies every section's Clamp()
        public SettingsSection DiffersFrom(GameSettings other) { throw new System.NotImplementedException("CORE-011"); }          // sections whose values differ
    }
}
