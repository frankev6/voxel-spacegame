using Unity.Mathematics;

namespace Game.Core
{
    /// <summary>Double-precision frame helpers (architecture §9). Burst-compatible. Implements SPEC-02 §4.10.</summary>
    public static class MathD
    {
        public static float3 ToLocal(double3 positionInStarSystem, double3 frameOrigin) => (float3)(positionInStarSystem - frameOrigin);  // (float3)(p - o)
        public static double3 ToStarSystem(float3 local, double3 frameOrigin) => frameOrigin + (double3)local;                            // o + (double3)local
        public static double DistanceSq(double3 a, double3 b) => math.distancesq(a, b);
        public static bool IsFinite(double3 v) => math.all(math.isfinite(v));
        public static bool IsFinite(float3 v) => math.all(math.isfinite(v));
    }
}
