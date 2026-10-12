namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public sealed class ConfirmRequest
    {
        public string Title;
        public string Body;
        public ConfirmOption[] Options;   // 1..3 options, left to right
        public int CancelIndex = -1;      // option chosen by Back (Esc); -1 = Back is consumed and ignored
        public int DefaultFocusIndex = 0;
    }
}
