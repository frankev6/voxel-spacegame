using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy nested object (no $ fields) (SPEC-72 §4.3).</summary>
    public sealed class LegacyModifierDto 
    {
        [JsonProperty("StatToModify")] public string StatToModify { get; set; }
        [JsonProperty("ModifyType")] public string ModifyType { get; set; }
        [JsonProperty("ModifierRange")] public string ModifierRange { get; set; }
        [JsonProperty("Value")] public double Value { get; set; }
    }
}
