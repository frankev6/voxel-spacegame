using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>StarEntity</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyStarEntityDto : LegacySystemEntityDto
    {
        [JsonProperty("minTemperature")] public double MinTemperature { get; set; }
        [JsonProperty("maxTemperature")] public double MaxTemperature { get; set; }
        [JsonProperty("minScale")] public double MinScale { get; set; }
        [JsonProperty("maxScale")] public double MaxScale { get; set; }
        [JsonProperty("minLuminosity")] public double MinLuminosity { get; set; }
        [JsonProperty("maxLuminosity")] public double MaxLuminosity { get; set; }
        [JsonProperty("minPlanetOrbitRadius")] public double MinPlanetOrbitRadius { get; set; }
        [JsonProperty("maxSystemRadius")] public double MaxSystemRadius { get; set; }
        [JsonProperty("galaxyMapPrefab")] public LegacyRef GalaxyMapPrefab { get; set; }
    }
}
