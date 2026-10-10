namespace Game.Core.Events
{
    /// <summary>Published by SPEC-05 SaveService after a successful restore (SPEC-05 §5.6 step 6). Implements SPEC-02 §4.5.</summary>
    public readonly struct GameLoaded
    {
        public readonly string ProfileId; public readonly string SlotId; public readonly int WarningCount;
        public GameLoaded(string profileId, string slotId, int warningCount) { ProfileId = profileId; SlotId = slotId; WarningCount = warningCount; }
    }
}
