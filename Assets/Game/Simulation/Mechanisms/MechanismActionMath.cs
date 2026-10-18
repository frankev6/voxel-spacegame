using Game.Simulation.Hazards;
using Unity.Mathematics;

namespace Game.Simulation.Mechanisms
{
    /// <summary>HAZ-014: how an owner applies an action to its own state. A mover keeps its pose continuous through every change.
    /// MoverPlay: Value &lt; 0 pauses, otherwise plays. MoverReverse: Value &gt; 0 runs backwards (Toggle: to the first point),
    /// &lt; 0 forwards (Toggle: to the second point), 0 flips. MoverSpeed: Rotate and Drift yaw rate rad/s, Rail m/s, Toggle travel s.
    /// ZonePeriod: Value = period s, keeping the on share.</summary>
    public static class MechanismActionMath
    {
        public static void Apply(ref Mover m, in MechanismAction a, uint tick)
        {
            switch (a.Kind)
            {
                case MechanismActionKind.MoverPlay:
                    if (a.Value < 0) { if (m.Playing != 0) { m.PausedTick = math.max(tick, 1); m.Playing = 0; } }
                    else if (m.Playing == 0)
                    {
                        m.StartTick = m.PausedTick != 0 ? m.StartTick + (tick - m.PausedTick) : tick;
                        m.PausedTick = 0; m.Playing = 1;
                    }
                    break;
                case MechanismActionKind.MoverReverse:
                {
                    byte want = a.Value > 0 ? (byte)1 : a.Value < 0 ? (byte)0 : (byte)(m.Reverse ^ 1);
                    if (want == m.Reverse) break;
                    uint at = m.Playing != 0 ? tick : m.PausedTick != 0 ? m.PausedTick : m.StartTick;
                    long elapsed = (long)at - m.StartTick;
                    switch (m.Path)
                    {
                        case MoverPath.Toggle:
                        {
                            // Keep the travel fraction: from e to (T − e), clamped to a finished travel.
                            long travel = (long)math.round(60 / math.max(m.Rate, 1e-4f));
                            long e = math.clamp(elapsed, 0, travel);
                            m.StartTick = (uint)math.max(0, at - (travel - e)); break;
                        }
                        case MoverPath.Rail: m.StartTick = (uint)math.max(0, 2L * at - m.StartTick); break;   // s(t) = −s(−t)
                        default:
                            Rebase(ref m, at); m.Rate = -m.Rate; m.Velocity = -m.Velocity; break;
                    }
                    m.Reverse = want;
                    break;
                }
                case MechanismActionKind.MoverSpeed:
                {
                    uint at = m.Playing != 0 ? tick : m.PausedTick != 0 ? m.PausedTick : m.StartTick;
                    long elapsed = (long)at - m.StartTick;
                    switch (m.Path)
                    {
                        case MoverPath.Rail:
                            if (math.abs(a.Value) > 1e-4f) m.StartTick = (uint)math.max(0, at - (long)math.round(elapsed * m.Speed / a.Value));
                            m.Speed = a.Value; break;
                        case MoverPath.Toggle:
                        {
                            float rate = 1 / math.max(a.Value, 1e-3f);
                            m.StartTick = (uint)math.max(0, at - (long)math.round(elapsed * m.Rate / rate)); m.Rate = rate; break;
                        }
                        default: Rebase(ref m, at); m.Rate = a.Value; break;
                    }
                    if (m.Playing == 0 && m.PausedTick != 0) m.PausedTick = at;
                    break;
                }
            }
        }

        /// <summary>Rotate, Drift: fold the time run so far into Yaw and Origin, so StartTick can move to <paramref name="at"/>.</summary>
        static void Rebase(ref Mover m, uint at)
        {
            double t = ((long)at - m.StartTick) * Game.Core.SimTime.FixedTimestepD;
            m.Yaw = MoverMath.Wrap(m.Yaw + math.fmod(m.Rate * t, 2 * math.PI_DBL));
            if (m.Path == MoverPath.Drift) m.Origin += (float3)((double3)m.Velocity * t);
            m.StartTick = at; if (m.PausedTick != 0) m.PausedTick = at;
        }

        public static void Apply(ref ZoneSchedule s, in MechanismAction a)
        {
            switch (a.Kind)
            {
                case MechanismActionKind.ZoneEnable: s.Enabled = 1; break;
                case MechanismActionKind.ZoneDisable: s.Enabled = 0; break;
                case MechanismActionKind.ZonePeriod:
                {
                    uint period = (uint)math.max(1, math.round(a.Value * 60));
                    float share = s.PeriodTicks == 0 ? .5f : (float)s.OnTicks / s.PeriodTicks;
                    s.PeriodTicks = period; s.OnTicks = (uint)math.round(period * share); break;
                }
            }
        }

        public static bool IsMover(MechanismActionKind k) => k == MechanismActionKind.MoverPlay || k == MechanismActionKind.MoverReverse || k == MechanismActionKind.MoverSpeed;
        public static bool IsZone(MechanismActionKind k) => k == MechanismActionKind.ZoneEnable || k == MechanismActionKind.ZoneDisable || k == MechanismActionKind.ZonePeriod;
    }
}
