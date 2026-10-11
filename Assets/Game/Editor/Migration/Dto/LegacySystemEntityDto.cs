using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>SolarSystemEntity</c> (SPEC-72 §4.3).</summary>
    public class LegacySystemEntityDto : LegacyDto
    {
        [JsonProperty("entityId")] public string EntityId { get; set; }
        [JsonProperty("ownerFactionIdOverride")] public string OwnerFactionIdOverride { get; set; }
        [JsonProperty("prefab")] public LegacyRef Prefab { get; set; }
        [JsonProperty("mapIcon")] public LegacyRef MapIcon { get; set; }
        [JsonProperty("color")] public double[] Color { get; set; }
        [JsonProperty("description")] public string Description { get; set; }
        [JsonProperty("encounterPool")] public LegacySystemEntityDto.EncounterPoolData EncounterPool { get; set; }
        [JsonProperty("encounterIdOverride")] public string EncounterIdOverride { get; set; }
        [JsonProperty("autoInteractOnContact")] public bool AutoInteractOnContact { get; set; }
        [JsonProperty("orbitType")] public string OrbitType { get; set; }
        [JsonProperty("orbitSpeedMultiplier")] public double OrbitSpeedMultiplier { get; set; }
        [JsonProperty("showOrbitLine")] public bool ShowOrbitLine { get; set; }
        [JsonProperty("spawnType")] public string SpawnType { get; set; }
        [JsonProperty("minOrbitRadius")] public double MinOrbitRadius { get; set; }
        [JsonProperty("maxOrbitRadius")] public double MaxOrbitRadius { get; set; }
        [JsonProperty("canHaveSatellites")] public bool CanHaveSatellites { get; set; }
        [JsonProperty("spawnRequirement")] public string SpawnRequirement { get; set; }
        [JsonProperty("spawnBlocker")] public string SpawnBlocker { get; set; }
        public sealed class EncounterPoolData { [JsonProperty("poolName")] public string PoolName { get; set; } [JsonProperty("encounters")] public WeightedEncounter[] Encounters { get; set; } }
        public sealed class WeightedEncounter { [JsonProperty("encounter")] public LegacyRef Encounter { get; set; } [JsonProperty("probabilityWeight")] public double ProbabilityWeight { get; set; } }
    }
}
