using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [System.Serializable] public sealed class AuthoredBody
    {
        public string key;                      // snake_case, unique in the star system, e.g. "ark_station"
        public BodyRole role;                   // Planet, Moon, Station, Belt or PointOfInterest
        public ushort slot;                     // unique per role in the star system
        public string parentKey;                // Moon: key of its planet; others: ""
        public BodyTemplateDefinition template;
        public OrbitMode mode = OrbitMode.Circular;   // Station/PointOfInterest/Belt must be Fixed
        public float orbitRadiusM;              // distance from parent (Belt: centre radius of the ring)
        public float phaseDegrees;              // angle at UniversalTime 0, measured from +X toward +Z
        public float eccentricity;              // Elliptic only, 0 .. 0.3
        public float periodOverrideS;           // 0 = Kepler-lite from the template (§5.5)
        public float radiusOverrideM;           // 0 = rolled from the body template range
        public float yawDegrees;                // Fixed bodies only
        public string displayNameOverride;      // "" = template display name
    }
}
