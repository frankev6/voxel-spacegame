namespace Game.Core.Events
{
    /// <summary>Global drain point for every EventBus&lt;T&gt; (SPEC-02 §4.5, §5.5).</summary>
    public static class EventHub
    {
        public const int MaxCascadePasses = 8;
        public static void DrainAll() { throw new System.NotImplementedException("CORE-006"); }                // delivers all pending events in publish order
        public static bool IsDraining => throw new System.NotImplementedException("CORE-006");
        public static int PendingCount => throw new System.NotImplementedException("CORE-006");                  // pending entries across all types (stale entries included)
        public static void Reset() { throw new System.NotImplementedException("CORE-006"); }                   // SubsystemRegistration and tests: clears all buses, handlers and pending events
    }
}
