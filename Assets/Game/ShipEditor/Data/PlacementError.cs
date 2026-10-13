namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public enum PlacementError : byte
    {
        None = 0, NotFound = 1, NotAvailable = 2, OrientationNotAllowed = 3, OutOfBounds = 4, Occupied = 5,
        InOtherClearance = 6, ClearanceBlocked = 7, NoAttachment = 8, CoreLimit = 9, OverBudget = 10,
        SymmetryOverlap = 11, SymmetryUnsupported = 12, MultiCellNotAllowed = 13
    }
}
