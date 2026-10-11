using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy nested object (no $ fields) (SPEC-72 §4.3).</summary>
    public sealed class LegacyShipVoxelDataDto 
    {
        [JsonProperty("AbilityIds")] public string[] AbilityIds { get; set; }
        [JsonProperty("OfficerIds")] public string[] OfficerIds { get; set; }
        [JsonProperty("GlobalStats")] public LegacyGlobalStatDto[] GlobalStats { get; set; }
        [JsonProperty("width")] public int Width { get; set; }
        [JsonProperty("height")] public int Height { get; set; }
        [JsonProperty("length")] public int Length { get; set; }
        [JsonProperty("voxels")] public LegacyVoxelDto[] Voxels { get; set; }
    }
}
