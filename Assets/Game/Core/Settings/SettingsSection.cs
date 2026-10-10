namespace Game.Core.Settings
{
    /// <summary>Settings sections, combinable (SPEC-02 §4.9).</summary>
    [System.Flags] public enum SettingsSection : byte { None = 0, Audio = 1, Video = 2, Controls = 4, Gameplay = 8, Accessibility = 16, All = 31 }
}
