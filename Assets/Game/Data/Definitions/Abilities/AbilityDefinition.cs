using Game.Data.Validation;

namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-24 §4.1. Authored ability granted by blocks; also referenced by AI ability rules (SPEC-23 Q6).</summary>
    [ContentIdPrefix("ability_")]
    [UnityEngine.CreateAssetMenu(menuName = "Game/Content/Ability", fileName = "ability_new")]
    public sealed class AbilityDefinition : ContentDefinition
    {
        [UnityEngine.SerializeField] private string displayName;
        [UnityEngine.SerializeField, UnityEngine.TextArea] private string description;
        [UnityEngine.SerializeField] private UnityEngine.Sprite icon;                 // placeholder until SPEC-81
        [UnityEngine.SerializeField] private AbilityKind kind;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float energyCost;     // E, one-shot
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float cooldownSeconds; // minimum gap between uses
        [UnityEngine.SerializeField, UnityEngine.Range(1, 9)] private int maxCharges = 1;
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float chargeRechargeSeconds; // time to restore one charge (maxCharges > 1)
        [UnityEngine.SerializeField, UnityEngine.Min(0)] private float durationSeconds; // StatBoost effect length
        [UnityEngine.SerializeField] private AbilityStatMod[] statModifiers;            // StatBoost: 1..4 entries
        [UnityEngine.SerializeField, UnityEngine.Range(0, 1)] private float shieldEnableThreshold; // ShieldToggle: min Hp/MaxHp to switch on
        [UnityEngine.SerializeField] private AudioCueDefinition activationCue;
        [UnityEngine.SerializeField] private FxDefinition activationFx;

        public string DisplayName => displayName;
        public string Description => description;
        public UnityEngine.Sprite Icon => icon;
        public AbilityKind Kind => kind;
        public float EnergyCost => energyCost;
        public float CooldownSeconds => cooldownSeconds;
        public int MaxCharges => maxCharges;
        public float ChargeRechargeSeconds => chargeRechargeSeconds;
        public float DurationSeconds => durationSeconds;
        public System.Collections.Generic.IReadOnlyList<AbilityStatMod> StatModifiers => statModifiers;
        public float ShieldEnableThreshold => shieldEnableThreshold;
        public AudioCueDefinition ActivationCue => activationCue;
        public FxDefinition ActivationFx => activationFx;

        /// <summary>SPEC-24 §7/Q3: kind rules plus finite authored values; ABL-E004 remains retired.</summary>
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (kind == AbilityKind.StatBoost)
            {
                if (statModifiers == null || statModifiers.Length < 1 || statModifiers.Length > 4)
                    scope.Error("ABL-E001", nameof(statModifiers), "StatBoost requires one to four modifiers.");
                if (statModifiers != null)
                    for (int i = 0; i < statModifiers.Length; i++)
                    {
                        var mod = statModifiers[i];
                        if (!System.Enum.IsDefined(typeof(Game.Data.Combat.StatId), mod.Stat)
                            || !float.IsFinite(mod.Multiplier) || mod.Multiplier < .01f)
                            scope.Error("ABL-E001", nameof(statModifiers), "Modifiers require a known stat and a finite multiplier >= 0.01.");
                        for (int j = 0; j < i; j++)
                            if (statModifiers[j].Stat == mod.Stat)
                            { scope.Error("ABL-E001", nameof(statModifiers), "A boost cannot modify the same stat twice."); break; }
                    }
                if (!float.IsFinite(durationSeconds) || durationSeconds <= 0 || Game.Core.SimTime.SecondsToTicks(durationSeconds) == 0)
                    scope.Error("ABL-E002", nameof(durationSeconds), "StatBoost requires a finite duration that rounds to at least one tick.");
            }
            if (maxCharges < 1 || maxCharges > 9 || (maxCharges > 1 && (!float.IsFinite(chargeRechargeSeconds) || chargeRechargeSeconds <= 0)))
                scope.Error("ABL-E003", nameof(chargeRechargeSeconds), "Charges must be 1..9; multiple charges need a finite positive recharge time.");
            if (kind == AbilityKind.ShieldToggle && durationSeconds > 0)
                scope.Error("ABL-E005", nameof(durationSeconds), "ShieldToggle must not have a duration; the effect owns its active state.");
            if (!System.Enum.IsDefined(typeof(AbilityKind), kind))
                scope.Error("ABL-E006", nameof(kind), "Unknown ability kind.");
            Nonnegative(scope, nameof(energyCost), energyCost);
            Nonnegative(scope, nameof(cooldownSeconds), cooldownSeconds);
            Nonnegative(scope, nameof(chargeRechargeSeconds), chargeRechargeSeconds);
            Nonnegative(scope, nameof(durationSeconds), durationSeconds);
            if (!float.IsFinite(cooldownSeconds * Game.Core.SimTime.TicksPerSecond)
                || !float.IsFinite(chargeRechargeSeconds * Game.Core.SimTime.TicksPerSecond))
                scope.Error("ABL-E006", nameof(cooldownSeconds), "Fractional tick timers must remain finite after conversion.");
            if (!float.IsFinite(shieldEnableThreshold) || shieldEnableThreshold < 0 || shieldEnableThreshold > 1)
                scope.Error("ABL-E006", nameof(shieldEnableThreshold), "Shield threshold must be finite and in [0,1].");
            if (context != null)
            {
                if (activationCue != null && !context.IsInDatabase(activationCue))
                    scope.Error("DATA-E010", nameof(activationCue), "Activation cue is not in the content database.");
                if (activationFx != null && !context.IsInDatabase(activationFx))
                    scope.Error("DATA-E010", nameof(activationFx), "Activation effect is not in the content database.");
            }
        }
        static void Nonnegative(ValidationScope scope, string field, float value)
        {
            if (!float.IsFinite(value) || value < 0)
                scope.Error("ABL-E006", field, "Value must be finite and nonnegative.");
        }
    }
}
