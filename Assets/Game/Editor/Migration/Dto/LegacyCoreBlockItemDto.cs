using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>CoreBlockItem</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyCoreBlockItemDto : LegacyBlockItemDto
    {
        [JsonProperty("MaxVoxelsSupported")] public int MaxVoxelsSupported { get; set; }
        [JsonProperty("MaxBuildDimensions")] public int MaxBuildDimensions { get; set; }
    }
}
