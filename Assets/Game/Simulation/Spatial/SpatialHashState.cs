namespace Game.Simulation.Spatial
{
    /// <summary>Implements SPEC-23 §4.1: SpatialHashState.</summary>
    public struct SpatialHashState
    {
        public float CellSize;           // 128 m
        public float LargeProxyRadius;   // 4 × CellSize
        public uint BuiltTick;           // SimClock.Tick of the last build
        public int ProxyCount, LargeCount, EntryCount, CellCount;
    }
}
