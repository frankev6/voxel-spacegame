using Game.Data.Definitions;
using Unity.Mathematics;

namespace Game.Galaxy.Data
{
    /// <summary>Implements SPEC-30 §4.5.</summary>
    public struct OrbitElements
    {
        public Game.Data.Definitions.OrbitMode Mode;
        public double RadiusM;          // Fixed: distance from parent; Circular/Elliptic: semi-major axis
        public double Eccentricity;     // Elliptic only, 0 .. 0.3
        public double PeriodS;          // Circular/Elliptic, > 0
        public double PhaseRad;         // angle (Fixed, Circular) or mean anomaly (Elliptic) at UniversalTime 0
        public double ArgPeriapsisRad;  // Elliptic only
    }
}
