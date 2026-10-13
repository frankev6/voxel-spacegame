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

        // TODO(ABL-002): validate authored fields and report ABL-E001, E002, E003 and E005 (E004 is retired).
        public override void Validate(ValidationScope scope, ContentValidationContext context)
            => throw new System.NotImplementedException("ABL-002");
    }
}
