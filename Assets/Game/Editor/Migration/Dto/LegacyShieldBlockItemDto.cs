using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>ShieldBlockItem</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyShieldBlockItemDto : LegacyBlockItemDto
    {
        [JsonProperty("ShieldRadius")] public double ShieldRadius { get; set; }
        [JsonProperty("ShieldForwardDirection")] public double[] ShieldForwardDirection { get; set; }
    }
}
