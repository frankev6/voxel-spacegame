namespace Game.Galaxy.Travel
{
    /// <summary>Implements SPEC-32 §4.4 travel diagnostics.</summary>
    public struct TravelStats
    {
        public int JumpsCompleted, JumpsAborted, CruisesCompleted, TransitTimeouts, RouteComputations;
        public float LastTransitSeconds, MaxTransitSeconds, LastRouteMs;
    }
}
