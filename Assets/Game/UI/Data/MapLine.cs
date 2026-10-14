namespace Game.UI.Data
{
    /// <summary>Map presentation data (SPEC-61 §4.2).</summary>
    public struct MapLine
    {
        public int A, B;
        public bool Gated;
        public bool Known;
        public bool Route; // HUD-016 / ADR-0112: this edge belongs to the selected route
    }
}
