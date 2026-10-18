using Game.Data.Hazards;
using Unity.Mathematics;

namespace Game.Simulation.Hazards
{
    /// <summary>HAZ-016 (ADR-0119 decision 4): when a Pulse zone fires and where its light comes from. Shared by PulseSystem (the
    /// authority's damage) and the pulse visual (every machine), so both agree on the tick and the source.</summary>
    public static class PulseMath
    {
        /// <summary>A pulse fires on the first tick of each on window of its zone's schedule; a zone with no schedule, or always on, never pulses.</summary>
        public static bool Fires(bool hasSchedule, in ZoneSchedule s, uint tick)
            => hasSchedule && s.Enabled != 0 && s.PeriodTicks > 0 && (tick + s.PhaseTicks) % s.PeriodTicks == 0;

        /// <summary>Whether a point lies in the zone (box or circle; a path zone never pulses).</summary>
        public static bool Inside(ZoneShape shape, float2 halfExtents, float2 centre, float yaw, float2 p)
        {
            float2 d = p - centre, forward = new float2(math.sin(yaw), math.cos(yaw)), right = new float2(forward.y, -forward.x);
            return shape switch
            {
                ZoneShape.Box => math.abs(math.dot(d, right)) <= halfExtents.x && math.abs(math.dot(d, forward)) <= halfExtents.y,
                ZoneShape.Circle => math.lengthsq(d) <= halfExtents.x * halfExtents.x,
                _ => false
            };
        }

        /// <summary>Where the pulse's light reaching <paramref name="target"/> starts: the zone centre (a reactor, C = 0), or far back along
        /// the zone's forward (a star, C = 1), at the target's height.</summary>
        public static float3 Source(float c, float2 halfExtents, float2 centre, float yaw, float3 target)
        {
            if (c < .5f) return new float3(centre.x, target.y, centre.y);
            float2 forward = new float2(math.sin(yaw), math.cos(yaw));
            float back = 2 * math.max(halfExtents.x, halfExtents.y);
            return target - new float3(forward.x, 0, forward.y) * back;
        }
    }
}
