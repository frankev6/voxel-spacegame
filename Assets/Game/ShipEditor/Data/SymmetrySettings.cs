namespace Game.ShipEditor.Data
{
    /// <summary>Implements SPEC-51 §4.2. Editor contract.</summary>
    public struct SymmetrySettings
    {
        public bool MirrorX, MirrorY, MirrorZ;
        public int PlaneX2, PlaneY2, PlaneZ2; // plane × 2 in cell units; odd = cell centre, even = boundary
        public byte RadialFold;              // 0 off, 2 or 4 (M4); >0 forces MirrorX/MirrorZ off
        public int RadialCenterX2, RadialCenterZ2;
        public int Version;                 // incremented on every change; preview cache key
    }
}
