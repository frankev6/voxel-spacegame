namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public enum EditChangeKind : byte
    {
        Applied = 0, Undone = 1, Redone = 2, Committed = 3, Discarded = 4, Opened = 5
    }
}
