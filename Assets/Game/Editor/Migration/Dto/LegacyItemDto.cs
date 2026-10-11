using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>ResourceItem</c> (SPEC-72 §4.3). Item name is ItemName in C# because LegacyDto.Name holds $name.</summary>
    public class LegacyItemDto : LegacyDto
    {
        [JsonProperty("RegistryKey")] public string RegistryKey { get; set; }
        [JsonProperty("Name")] public string ItemName { get; set; }
        [JsonProperty("Description")] public string Description { get; set; }
        [JsonProperty("BasePrice")] public int BasePrice { get; set; }
        [JsonProperty("isStackable")] public bool IsStackable { get; set; }
        [JsonProperty("ItemImage")] public LegacyRef ItemImage { get; set; }
        [JsonProperty("Category")] public string Category { get; set; }
        [JsonProperty("Rarity")] public string Rarity { get; set; }
        [JsonProperty("EffectType")] public string EffectType { get; set; }
        [JsonProperty("GrantsAbilities")] public LegacyRef[] GrantsAbilities { get; set; }
    }
}
