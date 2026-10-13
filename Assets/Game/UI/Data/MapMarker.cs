namespace Game.UI.Data
{
    /// <summary>Map presentation data (SPEC-61 §4.2).</summary>
    public struct MapMarker
    {
        public Unity.Mathematics.double2 Pos;
        public MapMarkerKind Kind;
        public float HeadingDeg;
        public string Label;
    }
}
