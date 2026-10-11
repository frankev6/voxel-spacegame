using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>SolarSystemData</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacySolarSystemDataDto : LegacyDto
    {
        [JsonProperty("systemId")] public string SystemId { get; set; }
        [JsonProperty("openWorldCoordinates")] public double[] OpenWorldCoordinates { get; set; }
        [JsonProperty("minEntities")] public int MinEntities { get; set; }
        [JsonProperty("maxEntities")] public int MaxEntities { get; set; }
        [JsonProperty("baseOrbitSpeed")] public double BaseOrbitSpeed { get; set; }
        [JsonProperty("orbitSpacingScale")] public double OrbitSpacingScale { get; set; }
        [JsonProperty("starEntities")] public LegacyRef[] StarEntities { get; set; }
        [JsonProperty("connectedSystems")] public LegacyRef[] ConnectedSystems { get; set; }
        [JsonProperty("entityPool")] public LegacySolarSystemDataDto.PoolEntry[] EntityPool { get; set; }
        [JsonProperty("maxAsteroidBelts")] public int MaxAsteroidBelts { get; set; }
        [JsonProperty("asteroidBeltEntity")] public LegacyRef AsteroidBeltEntity { get; set; }
        [JsonProperty("encounterOverride")] public LegacyRef EncounterOverride { get; set; }
        public sealed class PoolEntry { [JsonProperty("entity")] public LegacyRef Entity { get; set; } [JsonProperty("weight")] public double Weight { get; set; } [JsonProperty("maxCount")] public int MaxCount { get; set; } }
    }
}
