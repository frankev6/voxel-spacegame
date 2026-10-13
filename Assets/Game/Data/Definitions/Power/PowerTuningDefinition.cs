namespace Game.Data.Definitions
{
    /// <summary>Power and shield tunables. Exactly one asset (`tuning_power`) in the database. Implements SPEC-22 §4.5.</summary>
    [ContentIdPrefix("tuning_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Tuning/Power", fileName = "tuning_power")]
    public sealed class PowerTuningDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private byte priorityShieldEmitter = 200;
        [UnityEngine.SerializeField] private byte priorityThruster = 150;
        [UnityEngine.SerializeField] private byte priorityAbility = 120;
        [UnityEngine.SerializeField] private byte priorityWeapon = 100;
        [UnityEngine.SerializeField, UnityEngine.Min(1)] private int brownoutHoldTicks = 30;
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float shieldPaddingScale = 1.2f;     // × half extents
        [UnityEngine.SerializeField, UnityEngine.Min(0f)] private float shieldPaddingMetres = 2f;      // + per axis
        [UnityEngine.SerializeField, UnityEngine.Min(0.5f)] private float shieldMinRadiusMetres = 3f;
        [UnityEngine.SerializeField, UnityEngine.Range(0f, 1f)] private float shieldRestartFraction = 0.25f;
        [UnityEngine.SerializeField] private float shieldMultKinetic = 0.8f, shieldMultEnergy = 1.25f, shieldMultExplosive = 1f, shieldMultRam = 1f;
        [UnityEngine.SerializeField, UnityEngine.Min(1f)] private float sustainWarningSeconds = 30f;

        public byte PriorityShieldEmitter => priorityShieldEmitter;
        public byte PriorityThruster => priorityThruster;
        public byte PriorityAbility => priorityAbility;
        public byte PriorityWeapon => priorityWeapon;
        public int BrownoutHoldTicks => brownoutHoldTicks;
        public float ShieldPaddingScale => shieldPaddingScale;
        public float ShieldPaddingMetres => shieldPaddingMetres;
        public float ShieldMinRadiusMetres => shieldMinRadiusMetres;
        public float ShieldRestartFraction => shieldRestartFraction;
        public float ShieldMultKinetic => shieldMultKinetic;
        public float ShieldMultEnergy => shieldMultEnergy;
        public float ShieldMultExplosive => shieldMultExplosive;
        public float ShieldMultRam => shieldMultRam;
        public float SustainWarningSeconds => sustainWarningSeconds;

        /// <summary>Validates the authored ranges before baking (SPEC-22 PWR-003). Legacy shield multipliers are owned by DamageTableBlob.</summary>
        public override void Validate(Game.Data.Validation.ValidationScope scope, Game.Data.Validation.ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (brownoutHoldTicks < 1)
                scope.Error("PWR-E002", nameof(brownoutHoldTicks), "Brownout hold ticks must be at least 1.");
            Minimum(scope, nameof(shieldPaddingScale), shieldPaddingScale, 1f);
            Minimum(scope, nameof(shieldPaddingMetres), shieldPaddingMetres, 0f);
            Minimum(scope, nameof(shieldMinRadiusMetres), shieldMinRadiusMetres, 0.5f);
            if (!float.IsFinite(shieldRestartFraction) || shieldRestartFraction < 0f || shieldRestartFraction > 1f)
                scope.Error("PWR-E002", nameof(shieldRestartFraction), "Shield restart fraction must be finite and within [0, 1].");
            Minimum(scope, nameof(sustainWarningSeconds), sustainWarningSeconds, 1f);
        }

        static void Minimum(Game.Data.Validation.ValidationScope scope, string field, float value, float minimum)
        {
            if (!float.IsFinite(value) || value < minimum)
                scope.Error("PWR-E002", field, $"{field} must be finite and at least {minimum}.");
        }
    }
}
