namespace Game.Core
{
    /// <summary>Double-precision frame helpers (architecture §9). Burst-compatible. Implements SPEC-02 §4.10.</summary>
    public static class MathD
    {
        public static Unity.Mathematics.float3 ToLocal(Unity.Mathematics.double3 positionInStarSystem, Unity.Mathematics.double3 frameOrigin) { throw new System.NotImplementedException("CORE-010"); }  // (float3)(p - o)
        public static Unity.Mathematics.double3 ToStarSystem(Unity.Mathematics.float3 local, Unity.Mathematics.double3 frameOrigin) { throw new System.NotImplementedException("CORE-010"); }          // o + (double3)local
        public static double DistanceSq(Unity.Mathematics.double3 a, Unity.Mathematics.double3 b) { throw new System.NotImplementedException("CORE-010"); }
        public static bool IsFinite(Unity.Mathematics.double3 v) { throw new System.NotImplementedException("CORE-010"); }
        public static bool IsFinite(Unity.Mathematics.float3 v) { throw new System.NotImplementedException("CORE-010"); }
    }
}
