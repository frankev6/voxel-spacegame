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
        public System.Collections.Generic.IReadOnlyList<AiAbilityRule> AbilityRules => abilityRules;
        public override void Validate(ValidationScope scope, ContentValidationContext context) => throw new System.NotImplementedException();       // AI-E001..E003 (§7)
    }
}
