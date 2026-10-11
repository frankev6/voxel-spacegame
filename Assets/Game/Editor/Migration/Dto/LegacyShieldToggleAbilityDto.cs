using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>ShieldToggleAbilitySO</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyShieldToggleAbilityDto : LegacyDto
    {
        [JsonProperty("<ID>k__BackingField")] public string Id { get; set; }
        [JsonProperty("<Icon>k__BackingField")] public LegacyRef Icon { get; set; }
        [JsonProperty("<Description>k__BackingField")] public string Description { get; set; }
        [JsonProperty("enableThreshold")] public double EnableThreshold { get; set; }
    }
}
