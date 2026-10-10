namespace Game.Core.Settings
{
    /// <summary>Gameplay settings (SPEC-02 §4.9).</summary>
    public sealed class GameplaySettingsData
    {
        public int AutosaveIntervalMinutes = 5;      // one of {0, 5, 10, 15, 30}; 0 = off; other values snap to the nearest (ties → larger). 5 = SPEC-05 default 300 s
        public bool ShowControlHints = true;
        public void CopyFrom(GameplaySettingsData o) { throw new System.NotImplementedException("CORE-011"); }
        public void Clamp() { throw new System.NotImplementedException("CORE-011"); }
        public bool ValueEquals(GameplaySettingsData o) { throw new System.NotImplementedException("CORE-011"); }
    }
}
