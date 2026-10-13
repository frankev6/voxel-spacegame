namespace Game.Core.Events
{
    /// <summary>SPEC-32 §4.1 player-facing message, published only through managed EventBus; text may be managed.</summary>
    public readonly struct TravelNotice
    {
        public readonly TravelNoticeKind Kind;
        public readonly uint StarSystemValue; // 0 = none.
        public readonly string Text;
        public TravelNotice(TravelNoticeKind kind, uint starSystemValue, string text)
        { Kind = kind; StarSystemValue = starSystemValue; Text = text ?? ""; }
    }
}
