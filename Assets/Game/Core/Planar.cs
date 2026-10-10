namespace Game.Core
{
    /// <summary>XZ-plane helpers for planar flight (ADR-0003). Y up; yaw +θ turns +Z toward +X (clockwise seen from above). Burst-compatible.
    /// Implements SPEC-02 §4.10.</summary>
    public static class Planar
    {
        public const float RayEpsilon = 1e-6f;
        public static Unity.Mathematics.float2 XZ(Unity.Mathematics.float3 v) { throw new System.NotImplementedException("CORE-010"); }                        // (v.x, v.z)
        public static Unity.Mathematics.float3 FromXZ(Unity.Mathematics.float2 xz, float y = 0f) { throw new System.NotImplementedException("CORE-010"); }    // (xz.x, y, xz.y)
        public static Unity.Mathematics.float3 FlattenY(Unity.Mathematics.float3 v) { throw new System.NotImplementedException("CORE-010"); }                 // (v.x, 0, v.z)
        public static float YawFromForward(Unity.Mathematics.float3 forward) { throw new System.NotImplementedException("CORE-010"); }                        // atan2(f.x, f.z); 0 if |f.xz| < 1e-6
        public static Unity.Mathematics.float3 ForwardFromYaw(float yaw) { throw new System.NotImplementedException("CORE-010"); }                            // (sin yaw, 0, cos yaw)
        public static Unity.Mathematics.quaternion YawRotation(float yaw) { throw new System.NotImplementedException("CORE-010"); }                           // quaternion.RotateY(yaw)
        public static float WrapAngle(float radians) { throw new System.NotImplementedException("CORE-010"); }                                                // result in [-π, π)
        public static float SignedAngleXZ(Unity.Mathematics.float3 from, Unity.Mathematics.float3 to) { throw new System.NotImplementedException("CORE-010"); } // atan2(f.z*t.x - f.x*t.z, f.x*t.x + f.z*t.z); 0 if either |xz| < 1e-6
        public static bool RayPlaneY(Unity.Mathematics.float3 origin, Unity.Mathematics.float3 direction, float planeY,
                                     out Unity.Mathematics.float3 hit, out float distance) { throw new System.NotImplementedException("CORE-010"); }          // false if |dir.y| < RayEpsilon or t < 0
    }
}
