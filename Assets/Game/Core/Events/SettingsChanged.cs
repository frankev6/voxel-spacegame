namespace Game.Core.Events
{
    /// <summary>Settings sections changed by ISettingsService.Apply or ResetToDefaults (SPEC-02 §4.5).</summary>
    public readonly struct SettingsChanged { public readonly Game.Core.Settings.SettingsSection Sections; public SettingsChanged(Game.Core.Settings.SettingsSection sections) { Sections = sections; } }
}
