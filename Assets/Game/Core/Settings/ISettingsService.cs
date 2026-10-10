namespace Game.Core.Settings
{
    /// <summary>Implemented by SPEC-01 SettingsService. Implements SPEC-02 §4.9.</summary>
    public interface ISettingsService
    {
        GameSettings Current { get; }                                    // read-only by convention: never mutate
        GameSettings CreateEditableCopy();                               // Current.Clone()
        void Apply(GameSettings edited, SettingsSection sections);       // copy, clamp, publish SettingsChanged for changed sections, schedule save
        void ResetToDefaults(SettingsSection sections);
        void Flush();                                                    // write now if dirty
    }
}
