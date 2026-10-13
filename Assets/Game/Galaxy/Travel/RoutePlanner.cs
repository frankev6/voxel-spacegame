using Game.Galaxy.Data;

namespace Game.Galaxy.Travel
{
    /// <summary>SPEC-32 §4.6 deterministic shortest-path contract; TRV-004 supplies the job.</summary>
    public static class RoutePlanner
    {
        public static bool TryFindRoute(GalaxyDef g, RouteScratch scratch, int sourceIndex, int targetIndex, bool ignoreLocks,
            System.Collections.Generic.List<int> lanesOut, out double totalLy) => throw new System.NotImplementedException("TRV-004");
    }
}
