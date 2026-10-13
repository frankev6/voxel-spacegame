namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.7 heap key, ordered by (Dist, Hops, Id); TRV-004 supplies comparisons.</summary>
    internal struct RouteHeapEntry
    {
        public double Dist;
        public int Hops;
        public uint Id;
        public int Index;
    }
}
