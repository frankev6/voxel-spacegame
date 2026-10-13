using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-30 §4.3.</summary>
    [ContentIdPrefix("starsys_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/World/Authored Star System", fileName = "starsys_new")]
    public sealed class AuthoredStarSystemDefinition : ContentDefinition
    {
        [SerializeField] private string displayName;
        [SerializeField] private UnityEngine.Vector2 positionLy;           // galaxy frame
        [SerializeField] private StarSystemTemplateDefinition template;    // supplies orbit/period/lane-ring parameters
        [SerializeField] private BodyTemplateDefinition star;
        [SerializeField] private System.Collections.Generic.List<AuthoredBody> bodies = new();
        [SerializeField] private bool hiddenUntilKnown, isStory, isHub, noEncounters;
        [SerializeField] private bool connectProcedurally;                  // joins the procedural lane graph
        [SerializeField, UnityEngine.Range(0, 3)] private int danger;
        [SerializeField] private string startBodyKey;                       // start star system only: spawn near this body
        [SerializeField] private UnityEngine.Vector3 startOffsetM = new UnityEngine.Vector3(0f, 0f, -400f); // from that body at UniversalTime 0
        [SerializeField] private float startYawDegrees;

        public string DisplayName => displayName;
        public UnityEngine.Vector2 PositionLy => positionLy;
        public StarSystemTemplateDefinition Template => template;
        public BodyTemplateDefinition Star => star;
        public System.Collections.Generic.IReadOnlyList<AuthoredBody> Bodies => bodies;
        public bool HiddenUntilKnown => hiddenUntilKnown;
        public bool IsStory => isStory;
        public bool IsHub => isHub;
        public bool NoEncounters => noEncounters;
        public bool ConnectProcedurally => connectProcedurally;
        public int Danger => danger;
        public string StartBodyKey => startBodyKey;
        public UnityEngine.Vector3 StartOffsetM => startOffsetM;
        public float StartYawDegrees => startYawDegrees;

        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            WorldDefinitionValidation.Require(scope, "GAL-E010", nameof(template), template != null, "Authored star system requires its orbit/lane template.");
            WorldDefinitionValidation.Require(scope, "GAL-E010", nameof(star), star != null && star.Role == BodyRole.Star, "Authored star must use a Star template.");
            WorldDefinitionValidation.Require(scope, "GAL-E012", nameof(danger), danger >= 0 && danger <= 3, "Danger must be in 0..3.");
            WorldDefinitionValidation.Require(scope, "GAL-E013", nameof(positionLy), float.IsFinite(positionLy.x) && float.IsFinite(positionLy.y), "Galaxy position must be finite.");
            WorldDefinitionValidation.Require(scope, "GAL-E013", nameof(startOffsetM), float.IsFinite(startOffsetM.x) && float.IsFinite(startOffsetM.y) && float.IsFinite(startOffsetM.z), "Start offset must be finite.");
            WorldDefinitionValidation.Require(scope, "GAL-E008", nameof(startYawDegrees), float.IsFinite(startYawDegrees), "Start yaw must be finite.");
            var byKey = new System.Collections.Generic.Dictionary<string, AuthoredBody>(System.StringComparer.Ordinal);
            var slots = new System.Collections.Generic.HashSet<(BodyRole, ushort)>();
            if (bodies == null) return;
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i]; string path = $"bodies[{i}]";
                WorldDefinitionValidation.Require(scope, "GAL-E005", path, body != null, "Authored body must not be null.");
                if (body == null) continue;
                bool keyValid = WorldDefinitionValidation.BodyKey(body.key);
                WorldDefinitionValidation.Require(scope, "GAL-E005", path + ".key", keyValid, "Body key must be lowercase snake_case starting with a letter.");
                if (keyValid)
                    WorldDefinitionValidation.Require(scope, "GAL-E005", path + ".key", byKey.TryAdd(body.key, body), "Body key must be unique within this star system.");
                if (body.role != BodyRole.Moon)
                    WorldDefinitionValidation.Require(scope, "GAL-E005", path + ".slot", slots.Add((body.role, body.slot)), "Non-moon role/slot must be unique.");
                bool moving = body.role == BodyRole.Planet || body.role == BodyRole.Moon;
                bool fixedRole = body.role == BodyRole.Station || body.role == BodyRole.Belt || body.role == BodyRole.PointOfInterest;
                WorldDefinitionValidation.Require(scope, "GAL-E010", path + ".role", moving || fixedRole, "Authored body role must be Planet, Moon, Station, Belt or PointOfInterest.");
                WorldDefinitionValidation.Require(scope, "GAL-E010", path + ".template", body.template != null && body.template.Role == body.role, "Template must match the authored body role.");
                WorldDefinitionValidation.Require(scope, "GAL-E007", path + ".mode", moving ? body.mode == OrbitMode.Circular || body.mode == OrbitMode.Elliptic : body.mode == OrbitMode.Fixed, "Planet/moon must move; station, belt and POI must be fixed.");
                WorldDefinitionValidation.Require(scope, "GAL-E008", path + ".eccentricity", float.IsFinite(body.eccentricity) && body.eccentricity >= 0 && body.eccentricity <= .3f, "Eccentricity must be in 0..0.3.");
                WorldDefinitionValidation.Require(scope, "GAL-E008", path + ".orbitRadiusM", float.IsFinite(body.orbitRadiusM) && (body.orbitRadiusM > 0 || (body.role == BodyRole.PointOfInterest && body.mode == OrbitMode.Fixed && body.orbitRadiusM == 0)), "Orbit radius must be positive; fixed POI at zero is allowed.");
                WorldDefinitionValidation.Require(scope, "GAL-E008", path + ".periodOverrideS", float.IsFinite(body.periodOverrideS) && body.periodOverrideS >= 0, "Period override must be finite and nonnegative; zero uses Kepler-lite.");
                WorldDefinitionValidation.Require(scope, "GAL-E008", path + ".radiusOverrideM", float.IsFinite(body.radiusOverrideM) && body.radiusOverrideM >= 0, "Radius override must be finite and nonnegative; zero uses the template.");
                WorldDefinitionValidation.Require(scope, "GAL-E008", path + ".phaseDegrees", float.IsFinite(body.phaseDegrees), "Phase must be finite.");
                WorldDefinitionValidation.Require(scope, "GAL-E008", path + ".yawDegrees", float.IsFinite(body.yawDegrees), "Yaw must be finite.");
            }
            var moonCounts = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < bodies.Count; i++)
            {
                var body = bodies[i]; if (body == null) continue;
                string path = $"bodies[{i}].parentKey";
                if (body.role != BodyRole.Moon)
                {
                    WorldDefinitionValidation.Require(scope, "GAL-E006", path, string.IsNullOrEmpty(body.parentKey), "Only moons may name a parent planet.");
                    continue;
                }
                bool parentValid = !string.IsNullOrEmpty(body.parentKey) && byKey.TryGetValue(body.parentKey, out var parent) && parent.role == BodyRole.Planet;
                WorldDefinitionValidation.Require(scope, "GAL-E006", path, parentValid, "Moon parent must name a planet in this star system.");
                if (!parentValid) continue;
                // §5.1 derives Moon Slot from its parent and SubSlot from list order, not AuthoredBody.slot.
                moonCounts.TryGetValue(body.parentKey, out int count);
                WorldDefinitionValidation.Require(scope, "GAL-E005", $"bodies[{i}].slot", count <= ushort.MaxValue, "Derived moon subslot must fit ushort.");
                moonCounts[body.parentKey] = count + 1;
            }
        }
    }
}
