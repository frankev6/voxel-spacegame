using Unity.Entities;

namespace Game.Simulation.Hazards
{
    /// <summary>ADR-0119 decision 3: a zone of any kind switches on and off. On while Enabled and
    /// (tick + PhaseTicks) mod PeriodTicks &lt; OnTicks (PeriodTicks 0 = always on). Writers: ZoneSystem (the clock) and
    /// mechanism actions applied by ZoneSystem (SPEC-91 §4.6).</summary>
    public struct ZoneSchedule : IComponentData
    {
        public uint PeriodTicks, OnTicks, PhaseTicks;
        public byte Enabled;

        /// <summary>HAZ-015: whether the zone acts on <paramref name="tick"/>.</summary>
        public static bool IsOn(in ZoneSchedule s, uint tick)
            => s.Enabled != 0 && (s.PeriodTicks == 0 || (tick + s.PhaseTicks) % s.PeriodTicks < s.OnTicks);
    }
}
