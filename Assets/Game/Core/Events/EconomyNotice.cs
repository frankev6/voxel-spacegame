namespace Game.Core.Events
{
    /// <summary>SPEC-50 §4.2: managed, player-facing economy notice.</summary>
    public readonly struct EconomyNotice
    {
        public readonly EconomyNoticeKind Kind;
        public readonly string Text;
        public readonly ulong Pilot;   // ADR-0117: PersistentId of the co-op pilot it is for on another machine; 0 = this machine's player
        public EconomyNotice(EconomyNoticeKind kind, string text) { Kind = kind; Text = text ?? ""; Pilot = 0; }
        public EconomyNotice(EconomyNoticeKind kind, string text, ulong pilot) { Kind = kind; Text = text ?? ""; Pilot = pilot; }
    }
}
