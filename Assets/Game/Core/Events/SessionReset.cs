namespace Game.Core.Events
{
    /// <summary>Raised once when the session state is reset in place (architecture §7.3). Implements SPEC-02 §4.5.</summary>
    public readonly struct SessionReset { public readonly SessionResetReason Reason; public SessionReset(SessionResetReason reason) { Reason = reason; } }
}
