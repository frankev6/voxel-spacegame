using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>BoostAbilitySO</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyBoostAbilityDto : LegacyDto
    {
        [JsonProperty("<ID>k__BackingField")] public string Id { get; set; }
        [JsonProperty("<Icon>k__BackingField")] public LegacyRef Icon { get; set; }
        [JsonProperty("<Description>k__BackingField")] public string Description { get; set; }
        [JsonProperty("maxCharges")] public int MaxCharges { get; set; }
        [JsonProperty("cooldownPerUse")] public double CooldownPerUse { get; set; }
        [JsonProperty("chargeRechargeTime")] public double ChargeRechargeTime { get; set; }
        [JsonProperty("boostDuration")] public double BoostDuration { get; set; }
    }
}
