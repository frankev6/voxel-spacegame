using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>PlanetEntity</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyPlanetEntityDto : LegacySystemEntityDto
    {
        [JsonProperty("planetType")] public string PlanetType { get; set; }
        [JsonProperty("preferredOrbitDistance")] public double PreferredOrbitDistance { get; set; }
        [JsonProperty("minRadius")] public double MinRadius { get; set; }
        [JsonProperty("maxRadius")] public double MaxRadius { get; set; }
        [JsonProperty("hasAtmosphere")] public bool HasAtmosphere { get; set; }
        [JsonProperty("atmosphereColor")] public double[] AtmosphereColor { get; set; }
        [JsonProperty("hasRings")] public bool HasRings { get; set; }
        [JsonProperty("planetTextures")] public LegacyRef[] PlanetTextures { get; set; }
    }
}
