using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>OpenWorldGalaxyConfig</c> (SPEC-72 §4.3). Properties read from Import/Legacy/content/Galaxies/OpenWorldGalaxyConfig.json (MIG-001).</summary>
    public sealed class LegacyOpenWorldGalaxyConfigDto : LegacyDto
    {
        [JsonProperty("solarSystems")] public LegacyRef[] SolarSystems { get; set; }
        [JsonProperty("defaultHyperlaneEntity")] public LegacyRef DefaultHyperlaneEntity { get; set; }
        [JsonProperty("systemLoadRadius")] public double SystemLoadRadius { get; set; }
        [JsonProperty("systemUnloadRadius")] public double SystemUnloadRadius { get; set; }
        [JsonProperty("floatingOriginThreshold")] public double FloatingOriginThreshold { get; set; }
        [JsonProperty("solarSystemScaleMultiplier")] public double SolarSystemScaleMultiplier { get; set; }
        [JsonProperty("orbitDistanceMultiplier")] public double OrbitDistanceMultiplier { get; set; }
    }
}
