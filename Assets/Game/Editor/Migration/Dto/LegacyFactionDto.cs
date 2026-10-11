using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>FactionData</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyFactionDto : LegacyDto
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("displayName")] public string DisplayName { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("logo")] public LegacyRef Logo { get; set; }
        [JsonProperty("crest")] public LegacyRef Crest { get; set; }
        [JsonProperty("color")] public double[] Color { get; set; }
        [JsonProperty("shipNamePrefix")] public string ShipNamePrefix { get; set; }
        [JsonProperty("doctrine")] public LegacyFactionDto.DoctrineEntry[] Doctrine { get; set; }
        [JsonProperty("basePlayerRelation")] public int BasePlayerRelation { get; set; }
        public sealed class DoctrineEntry { [JsonProperty("shipKey")] public string ShipKey { get; set; } [JsonProperty("weight")] public double Weight { get; set; } }
    }
}
