using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy nested object (no $ fields) (SPEC-72 §4.3).</summary>
    public sealed class LegacyVoxelDto 
    {
        [JsonProperty("blockKey")] public string BlockKey { get; set; }
        [JsonProperty("moduleSlots")] public string[] ModuleSlots { get; set; }
        [JsonProperty("colorIndex")] public int ColorIndex { get; set; }
        [JsonProperty("x")] public int X { get; set; }
        [JsonProperty("y")] public int Y { get; set; }
        [JsonProperty("z")] public int Z { get; set; }
        [JsonProperty("rotX")] public double RotX { get; set; }
        [JsonProperty("rotY")] public double RotY { get; set; }
        [JsonProperty("rotZ")] public double RotZ { get; set; }
        [JsonProperty("IsDestroyed")] public bool IsDestroyed { get; set; }
    }
}
