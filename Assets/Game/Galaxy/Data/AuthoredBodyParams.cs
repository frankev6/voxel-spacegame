using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.4.</summary>
    public struct AuthoredBodyParams
    {
        public ulong LocalKey;                 // BodyId(role, slot, subSlot) of this entry; Moon SubSlot = index among the planet's moons in list order
        public ulong ParentKey;                // 0 = star
        public ushort TemplateIndex;
        public Game.Data.Definitions.OrbitMode Mode;
        public float OrbitRadiusM, PhaseRad, Eccentricity, PeriodOverrideS, RadiusOverrideM, YawRad;
    }
}
