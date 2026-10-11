namespace Game.Persistence.Data
{
    /// <summary>Implements SPEC-05 §4.5. Outcome of SaveAsync. The constructor is not in §4.5; readonly fields need one (SPEC-05 open question).</summary>
    public readonly struct SaveResult
    {
        public readonly bool Success; public readonly SaveErrorKind Error; public readonly string Message;
        public readonly SlotRef Slot; public readonly long Bytes; public readonly double CaptureMs; public readonly double TotalMs;
        public SaveResult(bool success, SaveErrorKind error, string message, SlotRef slot, long bytes, double captureMs, double totalMs)
        { Success = success; Error = error; Message = message; Slot = slot; Bytes = bytes; CaptureMs = captureMs; TotalMs = totalMs; }
    }
}
