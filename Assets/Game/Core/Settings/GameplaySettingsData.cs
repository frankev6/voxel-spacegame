namespace Game.Core.Settings
{
    /// <summary>Gameplay settings (SPEC-02 §4.9).</summary>
    public sealed class GameplaySettingsData
    {
        public int AutosaveIntervalMinutes = 5;      // one of {0, 5, 10, 15, 30}; 0 = off; other values snap to the nearest (ties → larger). 5 = SPEC-05 default 300 s
        public bool ShowControlHints = true;

        /// <summary>Nearest of {0, 5, 10, 15, 30}; ties → larger; negative → 0; above 30 → 30 (SPEC-02 §5.11).</summary>
        public static int SnapAutosave(int minutes)
        {
            if (minutes < 3) return 0;      // negative, 0..2 (2.5 is the midpoint to 5)
            if (minutes < 8) return 5;      // 3..7
            if (minutes < 13) return 10;    // 8..12
            if (minutes < 23) return 15;    // 13..22 (22.5 is the midpoint to 30)
            return 30;
        }

        public void CopyFrom(GameplaySettingsData o)
        {
            AutosaveIntervalMinutes = o.AutosaveIntervalMinutes; ShowControlHints = o.ShowControlHints;
        }
        public void Clamp()
        {
            AutosaveIntervalMinutes = SnapAutosave(AutosaveIntervalMinutes);
        }
        public bool ValueEquals(GameplaySettingsData o)
        {
            return AutosaveIntervalMinutes == o.AutosaveIntervalMinutes && ShowControlHints == o.ShowControlHints;
        }
    }
}
