#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Mathematics;
namespace Game.App.DevRuntime.Overlays
{
    /// <summary>Cached grid-local inclusive bounds of a six-connected solid island.</summary>
    internal struct IslandOverlayBounds
    {
        public int3 Min, Max;
        public int SolidCount, Index;
    }
}
#endif
