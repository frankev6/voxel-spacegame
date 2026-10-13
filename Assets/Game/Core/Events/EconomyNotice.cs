namespace Game.Core.Events
{
    /// <summary>SPEC-50 §4.2: managed, player-facing economy notice.</summary>
    public readonly struct EconomyNotice
    {
        public readonly EconomyNoticeKind Kind;
        public readonly string Text;
        public EconomyNotice(EconomyNoticeKind kind, string text) { Kind = kind; Text = text ?? ""; }
    }
}
