namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-23 §4.2: AiAbilityRule.</summary>
    [System.Serializable] public struct AiAbilityRule
    {
        public AbilityDefinition ability;            // pressed when the ship has a slot (0..3) with this ability that is Ready and not Blocked
        public AiAbilityTrigger trigger;
        [UnityEngine.Range(0, 1)] public float threshold; // HullBelow: MassFraction; ShieldBelow: Hp/MaxHp; else unused
    }
}
