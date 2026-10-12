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
        // TODO(AI-003): AI-E002 validation.
        public override void Validate(ValidationScope scope, ContentValidationContext context)
            => throw new System.NotImplementedException();
    }
}
