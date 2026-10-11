using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>PortalBlockItem</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyPortalBlockItemDto : LegacyBlockItemDto
    {
        [JsonProperty("EntryPosition")] public double[] EntryPosition { get; set; }
        [JsonProperty("EntryDirection")] public double[] EntryDirection { get; set; }
        [JsonProperty("ExitPosition")] public double[] ExitPosition { get; set; }
        [JsonProperty("ExitDirection")] public double[] ExitDirection { get; set; }
        [JsonProperty("EntryRadius")] public double EntryRadius { get; set; }
    }
}
