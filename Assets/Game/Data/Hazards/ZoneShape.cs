namespace Game.Data.Hazards
{
    /// <summary>Planar zone shapes (SPEC-91 §4.3). Path = capsule chain along a polyline. Append only.</summary>
    public enum ZoneShape : byte { Box = 0, Circle = 1, Path = 2 }
}
