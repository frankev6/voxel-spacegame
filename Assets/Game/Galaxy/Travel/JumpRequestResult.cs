namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.4 accepted/refused request; constructors normalize null reason text.</summary>
    public readonly struct JumpRequestResult
    {
        public readonly bool Accepted;
        public readonly JumpRefusal Refusal;
        public readonly string Reason;
        public JumpRequestResult(bool accepted, JumpRefusal refusal, string reason)
        { Accepted = accepted; Refusal = refusal; Reason = reason ?? ""; }
        public static JumpRequestResult Ok() => new JumpRequestResult(true, JumpRefusal.None, "");
        public static JumpRequestResult Refuse(JumpRefusal r, string reason) => new JumpRequestResult(false, r, reason);
    }
}
