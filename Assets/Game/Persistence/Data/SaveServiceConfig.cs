namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5.</summary>
    public sealed class SaveServiceConfig
    {
        public float AutosaveIntervalSeconds = 300f;     // unpaused gameplay seconds between timer autosaves
        public float AutosaveMinSpacingSeconds = 60f;    // real seconds; Timer/StarSystemEntered requests closer than this to the last save are dropped
        public int AutosaveSlotCount = 3;                // auto_0..auto_{n-1}
        public float BlockedRetrySeconds = 10f;          // real seconds between re-checks of a blocked pending autosave
        public float QuitSaveTimeoutSeconds = 10f;       // real seconds before quitting anyway
        public int MaxManualSlots = 999;
        public int ThumbnailWidth = 320, ThumbnailHeight = 180;
        public int MinCompressBytes = 64;
        public int MaxQueuedOperations = 4;
    }
}
