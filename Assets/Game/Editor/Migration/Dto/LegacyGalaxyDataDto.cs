using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>GalaxyData</c> (SPEC-72 §4.3). Properties read from Import/Legacy/content/Galaxies/Galaxy_01/GalaxyData_01.json (MIG-001).</summary>
    public sealed class LegacyGalaxyDataDto : LegacyDto
    {
        [JsonProperty("galaxyName")] public string GalaxyName { get; set; }
        [JsonProperty("primaryColor")] public double[] PrimaryColor { get; set; }
        [JsonProperty("secondaryColor")] public double[] SecondaryColor { get; set; }
        [JsonProperty("numLayers")] public int NumLayers { get; set; }
        [JsonProperty("nodesPerLayerMin")] public int NodesPerLayerMin { get; set; }
        [JsonProperty("nodesPerLayerMax")] public int NodesPerLayerMax { get; set; }
        [JsonProperty("layerRadiusStep")] public double LayerRadiusStep { get; set; }
        [JsonProperty("centerRadius")] public double CenterRadius { get; set; }
        [JsonProperty("nodePositionJitter")] public double[] NodePositionJitter { get; set; }
        [JsonProperty("wormholeCount")] public int WormholeCount { get; set; }
        [JsonProperty("minNodeDistance")] public double MinNodeDistance { get; set; }
        [JsonProperty("maxConnectionDistance")] public double MaxConnectionDistance { get; set; }
        [JsonProperty("seed")] public int Seed { get; set; }
        [JsonProperty("shape")] public string Shape { get; set; }
        [JsonProperty("spiralArms")] public int SpiralArms { get; set; }
        [JsonProperty("spiralTwist")] public double SpiralTwist { get; set; }
        [JsonProperty("ellipticalRatio")] public double EllipticalRatio { get; set; }
        [JsonProperty("lootTable")] public LegacyRef LootTable { get; set; }
        [JsonProperty("systemPool")] public LegacyGalaxyDataDto.SystemPoolEntry[] SystemPool { get; set; }
        [JsonProperty("boss")] public LegacyRef Boss { get; set; }
        [JsonProperty("startingSolarSystem")] public LegacyRef StartingSolarSystem { get; set; }
        [JsonProperty("generateFactionTerritories")] public bool GenerateFactionTerritories { get; set; }
        [JsonProperty("autoFactionCount")] public int AutoFactionCount { get; set; }
        [JsonProperty("factionTerritoryExpansionJitter")] public double FactionTerritoryExpansionJitter { get; set; }
        [JsonProperty("factionTerritoryCoverage")] public double FactionTerritoryCoverage { get; set; }
        [JsonProperty("factionNoOwnershipInnerLayers")] public int FactionNoOwnershipInnerLayers { get; set; }
        [JsonProperty("factionPool")] public LegacyRef[] FactionPool { get; set; }
        public sealed class SystemPoolEntry { [JsonProperty("data")] public LegacyRef Data { get; set; } [JsonProperty("weight")] public double Weight { get; set; } [JsonProperty("minCount")] public int MinCount { get; set; } [JsonProperty("maxCount")] public int MaxCount { get; set; } [JsonProperty("minDistance")] public double MinDistance { get; set; } }
    }
}
