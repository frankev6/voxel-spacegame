using Game.Core;
using Unity.Mathematics;

namespace Game.Simulation.Flight
{
    /// <summary>ADR-0123 decision 5: the heading controller of experimental 3D flight, shared by PropulsionJob and its tests.
    /// Unity's rotations: +yaw about Y turns the nose toward +X, +pitch about the right axis lowers it, +roll about the nose raises
    /// the right wing.</summary>
    public static class Flight3DMath
    {
        /// <summary>The nose's yaw (radians, atan2(x, z)) and pitch (radians, + = up) of a rotation.</summary>
        public static void YawPitch(quaternion rotation, out float yaw, out float pitch)
        {
            float3 forward = math.rotate(rotation, new float3(0, 0, 1));
            yaw = Planar.YawFromForward(forward);
            pitch = math.asin(math.clamp(forward.y, -1, 1));
        }

        /// <summary>Roll in radians: + = right wing up.</summary>
        public static float Roll(quaternion rotation) => math.asin(math.clamp(math.rotate(rotation, new float3(1, 0, 0)).y, -1, 1));

        /// <summary>The ship-frame target angular velocity (x = pitch axis, y = yaw axis, z = roll axis), rad/s.
        /// Facing (<paramref name="face"/> with a non-zero <paramref name="direction"/>): yaw and pitch close the angle to the direction
        /// at HeadingResponse per second, within the yaw and pitch caps, the pitch target clamped to ±MaxPitchDeg. Otherwise yaw follows
        /// <paramref name="yawRate"/> (Turn × cap, as on the plane) and the nose is levelled. Roll is always levelled.</summary>
        public static float3 TargetRates(in FlightConfig c, quaternion rotation, bool face, float3 direction, float yawRate, float yawCap)
        {
            YawPitch(rotation, out float yaw, out float pitch);
            float pitchCap = math.radians(c.MaxPitchRateDeg), maxPitch = math.radians(c.MaxPitchDeg);
            float pitchRate;
            if (face && math.lengthsq(direction) > 1e-8f)
            {
                float3 d = math.normalize(direction);
                // Straight up or down has no yaw: keep the current one.
                float wantYaw = math.lengthsq(d.xz) > 1e-8f ? math.atan2(d.x, d.z) : yaw;
                float wantPitch = math.clamp(math.asin(math.clamp(d.y, -1, 1)), -maxPitch, maxPitch);
                yawRate = math.clamp(Planar.WrapAngle(wantYaw - yaw) * c.HeadingResponse, -yawCap, yawCap);
                pitchRate = math.clamp((wantPitch - pitch) * c.HeadingResponse, -pitchCap, pitchCap);
            }
            else pitchRate = math.clamp(-pitch * c.HeadingResponse, -pitchCap, pitchCap);
            float rollRate = math.clamp(-Roll(rotation) * c.HeadingResponse, -pitchCap, pitchCap);

            // Yaw turns about the world up axis and pitch about the horizontal right axis, so the horizon stays put while the nose moves.
            float3 up = new float3(0, 1, 0);
            float3 forward = math.rotate(rotation, new float3(0, 0, 1));
            float3 right = math.normalizesafe(math.cross(up, new float3(forward.x, 0, forward.z)), math.rotate(rotation, new float3(1, 0, 0)));
            float3 local = math.rotate(math.inverse(rotation), up * yawRate - right * pitchRate);
            local.z += rollRate;
            return local;
        }

        /// <summary>The inverse inertia about a world axis, for a body whose inertia frame is <paramref name="inertiaRotation"/>.</summary>
        public static float InverseInertiaAbout(float3 inverseInertia, quaternion inertiaRotation, float3 worldAxis)
        {
            float3 a = math.rotate(math.inverse(inertiaRotation), worldAxis);
            return math.dot(inverseInertia, a * a);
        }
    }
}
