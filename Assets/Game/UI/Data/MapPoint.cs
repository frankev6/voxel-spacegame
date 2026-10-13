namespace Game.UI.Data
{
    /// <summary>Map presentation data (SPEC-61 §4.2).</summary>
    public struct MapPoint
    {
        public Unity.Mathematics.double2 Pos;
        public string Name;
        public float RadiusPx;
        public MapPointFlags Flags;
        public byte ColorIndex;
        public string ColorToken;
    }
}
