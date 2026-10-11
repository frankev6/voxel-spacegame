using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Base of every legacy DTO with $path/$type/$name (SPEC-72 §4.3).</summary>
    public abstract class LegacyDto
    {
        [JsonProperty("$path")] public string Path { get; set; }
        [JsonProperty("$type")] public string Type { get; set; }
        [JsonProperty("$name")] public string Name { get; set; }
    }
}
