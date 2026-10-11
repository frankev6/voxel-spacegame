namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. SaveReason and SaveBlockReason are SPEC-02's Game.Core.Save enums; this module declares no copies.</summary>
    public enum SaveErrorKind : byte { None = 0, Blocked = 1, IoError = 2, DiskFull = 3, ParticipantFailed = 4, Unreadable = 5, NewerFormat = 6, NotFound = 7, SlotLimit = 8 }
}
