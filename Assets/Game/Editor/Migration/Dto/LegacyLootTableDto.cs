using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>LootTable</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyLootTableDto : LegacyDto
    {
        [JsonProperty("lootItems")] public LegacyLootTableDto.LootItem[] LootItems { get; set; }
        public sealed class LootItem { [JsonProperty("item")] public LegacyRef Item { get; set; } [JsonProperty("minAmount")] public int MinAmount { get; set; } [JsonProperty("maxAmount")] public int MaxAmount { get; set; } [JsonProperty("dropChance")] public double DropChance { get; set; } }
    }
}
