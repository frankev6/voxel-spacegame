namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public enum EditIssueKind : byte
    {
        NoCore = 0, MultipleCores = 1, Disconnected = 2, OutOfBounds = 3, OverCellBudget = 4,
        DamageInvariant = 5, UnbuildableBlock = 6, // errors
        PowerDeficit = 100, NoThrust = 101, NoWeapons = 102 // warnings (≥ 100)
    }
}
