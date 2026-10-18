using Game.Core;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Simulation.Mechanisms
{
    /// <summary>HAZ-013: a mover's pose as a pure function of its path and the tick (ADR-0119 decision 1). No state, so a mover never
    /// drifts from its path however long it runs, and every machine computes the same pose.</summary>
    public static class MoverMath
    {
        /// <summary>Seconds along the path at the start of <paramref name="tick"/>, signed (a reversed rail can start "before" its
        /// StartTick). A paused mover holds the time it was paused at; one that never played holds its StartTick pose.</summary>
        public static double Seconds(in Mover m, uint tick)
        {
            long at = m.Playing != 0 ? tick : m.PausedTick != 0 ? m.PausedTick : m.StartTick;
            return (at - (long)m.StartTick) * SimTime.FixedTimestepD;
        }

        /// <summary>The pose at <paramref name="tick"/>.</summary>
        public static void Pose(in Mover m, NativeArray<MoverPoint> points, uint tick, out float3 position, out float yaw)
        {
            double t = Seconds(m, tick);
            switch (m.Path)
            {
                case MoverPath.Rotate:
                    position = m.Origin; yaw = Wrap(m.Yaw + math.fmod(m.Rate * t, 2 * math.PI_DBL)); return;
                case MoverPath.Drift:
                    position = m.Origin + (float3)((double3)m.Velocity * t); yaw = Wrap(m.Yaw + math.fmod(m.Rate * t, 2 * math.PI_DBL)); return;
                case MoverPath.Toggle:
                    if (points.Length < 2) { position = m.Origin; yaw = m.Yaw; return; }
                    float u = (float)math.saturate(m.Rate * t); if (m.Reverse != 0) u = 1 - u;
                    position = math.lerp(points[0].Pose, points[1].Pose, u); yaw = LerpYaw(points[0].Yaw, points[1].Yaw, u); return;
                default:
                    Rail(m, points, t, out position, out yaw); return;
            }
        }

        static void Rail(in Mover m, NativeArray<MoverPoint> points, double t, out float3 position, out float yaw)
        {
            if (points.Length == 0) { position = m.Origin; yaw = m.Yaw; return; }
            if (points.Length == 1) { position = points[0].Pose; yaw = points[0].Yaw; return; }
            int segments = m.PingPong != 0 ? points.Length - 1 : points.Length;
            double length = 0;
            for (int i = 0; i < segments; i++) length += math.distance(points[i].Pose, points[(i + 1) % points.Length].Pose);
            if (!(length > 1e-6)) { position = points[0].Pose; yaw = points[0].Yaw; return; }
            double s = m.Speed * t * (m.Reverse != 0 ? -1 : 1);
            double span = m.PingPong != 0 ? 2 * length : length;
            s -= math.floor(s / span) * span;
            if (m.PingPong != 0 && s > length) s = span - s;
            for (int i = 0; i < segments; i++)
            {
                var a = points[i]; var b = points[(i + 1) % points.Length];
                double d = math.distance(a.Pose, b.Pose);
                if (s <= d || i == segments - 1)
                {
                    float u = d > 1e-9 ? (float)math.saturate(s / d) : 0;
                    position = math.lerp(a.Pose, b.Pose, u); yaw = LerpYaw(a.Yaw, b.Yaw, u); return;
                }
                s -= d;
            }
            position = points[0].Pose; yaw = points[0].Yaw;
        }

        public static float LerpYaw(float a, float b, float u)
        {
            float d = Wrap(b - a);
            return Wrap(a + d * u);
        }

        /// <summary>An angle in [−π, π).</summary>
        public static float Wrap(double a)
        {
            a -= math.floor((a + math.PI_DBL) / (2 * math.PI_DBL)) * 2 * math.PI_DBL;
            return (float)a;
        }
    }
}
