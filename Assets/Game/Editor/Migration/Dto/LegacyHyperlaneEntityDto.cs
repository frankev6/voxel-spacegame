using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>Assets.Game.Core.Galaxy_Map.HyperlaneEntity</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyHyperlaneEntityDto : LegacySystemEntityDto
    {
        [JsonProperty("routeStrength")] public double RouteStrength { get; set; }
        [JsonProperty("duration")] public double Duration { get; set; }
    }
}
