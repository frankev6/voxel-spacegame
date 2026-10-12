namespace Game.UI.Data
{
    /// <summary>Implements SPEC-60 §4.1. Shared UI contract.</summary>
    public enum LayoutIssueKind : byte { OutOfViewport = 0, RegionOverlap = 1, TextOverflow = 2, OutsidePanel = 3 }
}
