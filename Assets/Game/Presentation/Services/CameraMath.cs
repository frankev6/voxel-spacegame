using Unity.Mathematics;

namespace Game.Presentation.Services
{
    /// <summary>Pure camera damping and orbit math (SPEC-62 §3, §5.3). Half-lives are positive; dt and lengths are nonnegative.</summary>
    public static class CameraMath
    {
        public static float Damp(float current, float target, float halfLife, float dt)
            => math.lerp(target, current, math.exp2(-dt / halfLife));

        public static float3 Damp(float3 current, float3 target, float halfLife, float dt)
            => math.lerp(target, current, math.exp2(-dt / halfLife));

        public static float DampAngle(float deg, float targetDeg, float halfLife, float dt)
            => WrapDegrees(Damp(deg, deg + WrapDegrees(targetDeg - deg), halfLife, dt));

        static float WrapDegrees(float degrees)
        {
            degrees %= 360;
            if (degrees <= -180) degrees += 360;
            else if (degrees > 180) degrees -= 360;
            return degrees;
        }

        // World yaw, then local pitch: positive X rotation looks down at the XZ plane.
        public static quaternion OrbitRotation(float pitchDeg, float yawDeg)
            => math.mul(quaternion.RotateY(math.radians(yawDeg)), quaternion.RotateX(math.radians(pitchDeg)));

        public static float3 OrbitPosition(float3 focus, quaternion r, float distance)
            => focus - math.rotate(r, new float3(0, 0, distance));

        public static float3 ClampLength(float3 v, float max)
        {
            float lengthSq = math.lengthsq(v);
            return lengthSq > max * max ? v * (max / math.sqrt(lengthSq)) : v;
        }

        public static float HeadingYawDeg(quaternion rot)
        {
            float3 forward = math.rotate(rot, new float3(0, 0, 1));
            return math.degrees(math.atan2(forward.x, forward.z));
        }
    }
}
