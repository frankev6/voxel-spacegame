using Game.Core.Save;

namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. Raised for every save and load outcome except blocked autosaves (§3 decision 14).</summary>
    public readonly struct SaveOperationResult
    {
        public readonly bool IsLoad; public readonly bool Success; public readonly SaveErrorKind Error;
        public readonly string Message;             // player-facing English (§5.8)
        public readonly SlotRef Slot; public readonly SaveReason Reason;
        public readonly int WarningCount;           // loads only
        public readonly Game.Data.Validation.ValidationReport Report;   // loads only; null for saves
        public SaveOperationResult(bool isLoad, bool success, SaveErrorKind error, string message, SlotRef slot, SaveReason reason,
                                   int warningCount, Game.Data.Validation.ValidationReport report)
        { IsLoad = isLoad; Success = success; Error = error; Message = message; Slot = slot; Reason = reason; WarningCount = warningCount; Report = report; }
    }
}
