using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>ShipItem</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyShipItemDto : LegacyItemDto
    {
        [JsonProperty("ShipVoxelData")] public LegacyShipVoxelDataDto ShipVoxelData { get; set; }
        [JsonProperty("Thumbnail")] public LegacyRef Thumbnail { get; set; }
    }
}
