namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct CommitResult
    {
        public CommitStatus Status;
        public string Message; // human-readable toast/prompt
        public int ItemsUsed, ItemsRefunded; // 0 unless committed
    }
}
