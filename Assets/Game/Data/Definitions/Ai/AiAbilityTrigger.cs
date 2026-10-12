namespace Game.Data.Definitions
{
    /// <summary>Implements SPEC-23 §4.2: AiAbilityTrigger.</summary>
    public enum AiAbilityTrigger : byte { EngageStart = 0, Fleeing = 1, HullBelow = 2, ShieldBelow = 3, ShieldOff = 4, HasTarget = 5 }
}
