using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-23 §4.2: AiConfigDefinition.</summary>
    [ContentIdPrefix("config_")]
    public sealed class AiConfigDefinition : ContentDefinition      // single instance: config_ai
    {
        [UnityEngine.SerializeField, UnityEngine.Min(32)] private float hashCellSize = 128;
        [UnityEngine.SerializeField, UnityEngine.Range(1, 20)] private int dormantThinkMultiplier = 6;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float combatBubbleRadius = 2000;    // m: PlayerCombatStatus counts engaging hostiles within this
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private float arrivalRadius = 40;           // m
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float avoidanceMargin = 25;         // m added to radii for local avoidance
        [UnityEngine.SerializeField] private AiProfileDefinition defaultProfile;                     // used when AiSetup gets ContentId.None
        public float HashCellSize => hashCellSize;
        public int DormantThinkMultiplier => dormantThinkMultiplier;
        public float CombatBubbleRadius => combatBubbleRadius;
        public float ArrivalRadius => arrivalRadius;
        public float AvoidanceMargin => avoidanceMargin;
        public AiProfileDefinition DefaultProfile => defaultProfile;
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            Minimum(scope, nameof(hashCellSize), hashCellSize, 32f);
            if (dormantThinkMultiplier < 1 || dormantThinkMultiplier > 20)
                scope.Error("AI-E002", nameof(dormantThinkMultiplier), "Dormant think multiplier must be within [1, 20].");
            Minimum(scope, nameof(combatBubbleRadius), combatBubbleRadius, 0f);
            Minimum(scope, nameof(arrivalRadius), arrivalRadius, 1f);
            Minimum(scope, nameof(avoidanceMargin), avoidanceMargin, 0f);
            if (defaultProfile == null)
                scope.Error("AI-E002", nameof(defaultProfile), "An AI config needs a default profile.");
            else if (context?.Catalog != null && context.Catalog.IndexOf(defaultProfile) == 0)
                scope.Error("AI-E002", nameof(defaultProfile), "The default AI profile must be in the content catalog.");
        }

        static void Minimum(ValidationScope scope, string field, float value, float minimum)
        {
            if (!float.IsFinite(value) || value < minimum)
                scope.Error("AI-E002", field, $"{field} must be finite and at least {minimum}.");
        }
    }
}
