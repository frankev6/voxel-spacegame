namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public enum CommitStatus : byte
    {
        Committed = 0, NothingToCommit = 1, Invalid = 2, MissingItems = 3, CargoFull = 4, TargetRejected = 5
    }
}
