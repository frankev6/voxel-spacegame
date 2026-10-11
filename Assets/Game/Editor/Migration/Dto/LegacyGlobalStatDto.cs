using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy nested object (no $ fields) (SPEC-72 §4.3).</summary>
    public sealed class LegacyGlobalStatDto 
    {
        [JsonProperty("Type")] public string Type { get; set; }
        [JsonProperty("Value")] public LegacyGlobalStatDto.StatValue Value { get; set; }
        public sealed class StatValue { [JsonProperty("Additive")] public double Additive { get; set; } [JsonProperty("Multiplicative")] public double Multiplicative { get; set; } }
    }
}
