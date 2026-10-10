namespace Game.Core.Save
{
    /// <summary>Any malformed save read (SPEC-02 §4.7, Decision 12).</summary>
    public sealed class SaveFormatException : System.Exception
    {
        public readonly int Position;                        // reader position at failure, -1 if unknown
        public SaveFormatException(string message, int position) : base(message + " (at byte " + position + ")") { Position = position; }
    }
}
