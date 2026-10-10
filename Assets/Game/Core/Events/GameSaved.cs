namespace Game.Core.Events
{
    /// <summary>Published by SPEC-05 SaveService after a successful save (SPEC-05 §5.4 step 17). Implements SPEC-02 §4.5.</summary>
    public readonly struct GameSaved
    {
        public readonly string ProfileId; public readonly string SlotId; public readonly Game.Core.Save.SaveReason Reason;   // e.g. "p001", "manual_001"
        public GameSaved(string profileId, string slotId, Game.Core.Save.SaveReason reason) { ProfileId = profileId; SlotId = slotId; Reason = reason; }
    }
}
