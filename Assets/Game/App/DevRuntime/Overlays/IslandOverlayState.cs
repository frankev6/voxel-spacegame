#if UNITY_EDITOR || DEVELOPMENT_BUILD
namespace Game.App.DevRuntime.Overlays
{
    /// <summary>Flood result, including the largest island even when other bounds are truncated.</summary>
    internal struct IslandOverlayState
    {
        public int Count, Skipped;
        public IslandOverlayBounds Largest;
    }
}
#endif
