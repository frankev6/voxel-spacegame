using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-23 §4.2: AiProfileDefinition.</summary>
    [ContentIdPrefix("ai_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/AI Profile", fileName = "ai_new")]
    public sealed class AiProfileDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private AiRole role;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 120)] private int thinkIntervalTicks = 10;
        [UnityEngine.SerializeField, UnityEngine.Min(100)] private float sensorRange = 1500;          // m: target search radius
        [UnityEngine.SerializeField, UnityEngine.Range(0.2f, 1.2f)] private float preferredRangeFraction = 0.7f; // of WeaponStats.MaxWeaponRange (SPEC-21)
        [UnityEngine.SerializeField, UnityEngine.Min(10)] private float minEngageRange = 80;           // m
        [UnityEngine.SerializeField] private AiFacing facing = AiFacing.FaceTarget;
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1)] private float throttle = 0.85f;         // max fraction of the speed caps
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1)] private float fleeHullFraction = 0.25f; // ShipStats.MassFraction below which it flees; 0 = never
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float leashRadius = 3000;            // m from the anchor; 0 = no leash
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float patrolRadius = 600;            // m around the anchor
        [UnityEngine.SerializeField, UnityEngine.Range(0, 2)] private float playerBias = 0.3f;        // score bonus for targeting the player
        [UnityEngine.SerializeField, UnityEngine.Range(0, 2)] private float stickiness = 0.25f;       // score bonus for the current target
        [UnityEngine.SerializeField, UnityEngine.Range(0, 60)] private int orbitFlipThinks = 12;      // thinks between orbit-direction flips (0 = never)
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1.5f)] private float orbitStepRad = 0.35f;  // angle advanced around the target per think
        [UnityEngine.SerializeField] private bool avoidFriendlyFire = true;
        [UnityEngine.SerializeField] private AiAbilityRule[] abilityRules = System.Array.Empty<AiAbilityRule>();   // ≤ 8, checked in order (review X5)
        public AiRole Role => role;
        public int ThinkIntervalTicks => thinkIntervalTicks;
        public float SensorRange => sensorRange;
        public float PreferredRangeFraction => preferredRangeFraction;
        public float MinEngageRange => minEngageRange;
        public AiFacing Facing => facing;
        public float Throttle => throttle;
        public float FleeHullFraction => fleeHullFraction;
        public float LeashRadius => leashRadius;
        public float PatrolRadius => patrolRadius;
        public float PlayerBias => playerBias;
        public float Stickiness => stickiness;
        public int OrbitFlipThinks => orbitFlipThinks;
        public float OrbitStepRad => orbitStepRad;
        public bool AvoidFriendlyFire => avoidFriendlyFire;
        public System.Collections.Generic.IReadOnlyList<AiAbilityRule> AbilityRules => abilityRules ?? System.Array.Empty<AiAbilityRule>();

        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (!System.Enum.IsDefined(typeof(AiRole), role)) scope.Error("AI-E001", nameof(role), "Unknown AI role.");
            if (!System.Enum.IsDefined(typeof(AiFacing), facing)) scope.Error("AI-E001", nameof(facing), "Unknown AI facing mode.");
            if (thinkIntervalTicks < 1 || thinkIntervalTicks > 120)
                scope.Error("AI-E001", nameof(thinkIntervalTicks), "Think interval must be within [1, 120] ticks.");
            Minimum(scope, nameof(sensorRange), sensorRange, 100f);
            Range(scope, nameof(preferredRangeFraction), preferredRangeFraction, .2f, 1.2f);
            Minimum(scope, nameof(minEngageRange), minEngageRange, 10f);
            Range(scope, nameof(throttle), throttle, 0f, 1f);
            Range(scope, nameof(fleeHullFraction), fleeHullFraction, 0f, 1f);
            Minimum(scope, nameof(leashRadius), leashRadius, 0f);
            Minimum(scope, nameof(patrolRadius), patrolRadius, 0f);
            Range(scope, nameof(playerBias), playerBias, 0f, 2f);
            Range(scope, nameof(stickiness), stickiness, 0f, 2f);
            if (orbitFlipThinks < 0 || orbitFlipThinks > 60)
                scope.Error("AI-E001", nameof(orbitFlipThinks), "Orbit flip interval must be within [0, 60] thinks.");
            Range(scope, nameof(orbitStepRad), orbitStepRad, 0f, 1.5f);
            if (AbilityRules.Count > 8)
                scope.Error("AI-E003", nameof(abilityRules), "At most eight ability rules are allowed.");
            for (int i = 0; i < AbilityRules.Count; i++)
            {
                var rule = AbilityRules[i];
                string field = $"{nameof(abilityRules)}[{i}]";
                if (rule.ability == null)
                    scope.Error("AI-E003", field + ".ability", "An ability rule needs an ability definition.");
                if (!System.Enum.IsDefined(typeof(AiAbilityTrigger), rule.trigger))
                    scope.Error("AI-E003", field + ".trigger", "Unknown ability trigger.");
                if (!float.IsFinite(rule.threshold) || rule.threshold < 0f || rule.threshold > 1f)
                    scope.Error("AI-E003", field + ".threshold", "Ability threshold must be finite and within [0, 1].");
            }
        }

        static void Minimum(ValidationScope scope, string field, float value, float minimum)
        {
            if (!float.IsFinite(value) || value < minimum)
                scope.Error("AI-E001", field, $"{field} must be finite and at least {minimum}.");
        }
        static void Range(ValidationScope scope, string field, float value, float minimum, float maximum)
        {
            if (!float.IsFinite(value) || value < minimum || value > maximum)
                scope.Error("AI-E001", field, $"{field} must be finite and within [{minimum}, {maximum}].");
        }
    }
}
