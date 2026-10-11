using Unity.Mathematics;

namespace Game.Core
{
    /// <summary>XZ-plane helpers for planar flight (ADR-0003). Y up; yaw +θ turns +Z toward +X (clockwise seen from above). Burst-compatible.
    /// Implements SPEC-02 §4.10 and §5.10.</summary>
    public static class Planar
    {
        public const float RayEpsilon = 1e-6f;
        const float XzEpsilon = 1e-6f;

        public static float2 XZ(float3 v) => new float2(v.x, v.z);
        public static float3 FromXZ(float2 xz, float y = 0f) => new float3(xz.x, y, xz.y);
        public static float3 FlattenY(float3 v) => new float3(v.x, 0f, v.z);

        /// <summary>atan2(f.x, f.z); 0 if |f.xz| &lt; 1e-6.</summary>
        public static float YawFromForward(float3 forward)
            => math.length(XZ(forward)) < XzEpsilon ? 0f : math.atan2(forward.x, forward.z);

        public static float3 ForwardFromYaw(float yaw) => new float3(math.sin(yaw), 0f, math.cos(yaw));
        public static quaternion YawRotation(float yaw) => quaternion.RotateY(yaw);

        /// <summary>Result in [-π, π): x - 2π·floor((x + π) / 2π); π maps to -π.</summary>
        public static float WrapAngle(float radians) => radians - 2f * math.PI * math.floor((radians + math.PI) / (2f * math.PI));

        /// <summary>atan2(f.z·t.x - f.x·t.z, f.x·t.x + f.z·t.z), positive clockwise seen from above; 0 if either |xz| &lt; 1e-6.</summary>
        public static float SignedAngleXZ(float3 from, float3 to)
        {
            if (math.length(XZ(from)) < XzEpsilon || math.length(XZ(to)) < XzEpsilon) return 0f;
            return math.atan2(from.z * to.x - from.x * to.z, from.x * to.x + from.z * to.z);
        }

        /// <summary>Intersection with the plane y = planeY; false if |dir.y| &lt; RayEpsilon or the plane is behind the origin.</summary>
        public static bool RayPlaneY(float3 origin, float3 direction, float planeY, out float3 hit, out float distance)
        {
            hit = default; distance = 0f;
            if (math.abs(direction.y) < RayEpsilon) return false;
            float t = (planeY - origin.y) / direction.y;
            if (t < 0f) return false;
            hit = origin + direction * t;
            distance = t * math.length(direction);
            return true;
        }
    }
}
