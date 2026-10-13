namespace Game.UI.Data
{
    /// <summary>Map presentation data (SPEC-61 §4.2).</summary>
    [System.Flags]
    public enum MapPointFlags : byte
    {
        None = 0,
        Known = 1,
        Visited = 2,
        Current = 4,
        Destination = 8,
        Selected = 16,
        Interactable = 32
    }
}
