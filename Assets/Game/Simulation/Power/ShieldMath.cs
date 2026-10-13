using System;
using Unity.Mathematics;

namespace Game.Simulation.Power
{
    /// <summary>Implements SPEC-22 §4.4. Pure, allocation-free, Burst-compatible ellipsoid queries in grid space.</summary>
    public static class ShieldMath
    {
        /// <summary>True only if p0→p1 starts outside and enters; t is the entry fraction in [0,1]. Shots from inside pass out.</summary>
        public static bool SegmentEnters(float3 p0, float3 p1, float3 center, float3 radii, out float t)
            => throw new NotImplementedException("PWR-002");

        /// <summary>True if p is inside or on the ellipsoid.</summary>
        public static bool Contains(float3 p, float3 center, float3 radii)
            => throw new NotImplementedException("PWR-002");

        /// <summary>Conservative test: true if a sphere with centre c and radius r may overlap the ellipsoid.</summary>
        public static bool SphereOverlaps(float3 c, float r, float3 center, float3 radii)
            => throw new NotImplementedException("PWR-002");

        /// <summary>Surface point along the ray from center towards p; p must differ from center.</summary>
        public static float3 SurfacePointToward(float3 p, float3 center, float3 radii)
            => throw new NotImplementedException("PWR-002");

        /// <summary>Outward unit normal at a surface point.</summary>
        public static float3 Normal(float3 surfacePoint, float3 center, float3 radii)
            => throw new NotImplementedException("PWR-002");
    }
}
