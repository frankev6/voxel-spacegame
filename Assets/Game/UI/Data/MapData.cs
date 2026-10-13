using System.Collections.Generic;

namespace Game.UI.Data
{
    /// <summary>Map presentation data (SPEC-61 §4.2).</summary>
    public sealed class MapData
    {
        public readonly List<MapPoint> Points = new List<MapPoint>(1024);   // stars or bodies
        public readonly List<MapLine> Lines = new List<MapLine>(4096);      // lanes (galaxy); unused for star systems
        public readonly List<MapRing> Rings = new List<MapRing>(64);        // orbits (star system)
        public readonly List<MapMarker> Markers = new List<MapMarker>(64);  // player, destination, rumors
        public Unity.Mathematics.double2 BoundsMin, BoundsMax;              // map units
    }
}
