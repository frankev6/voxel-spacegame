using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>SlottableItem</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacySlottableItemDto : LegacyItemDto
    {
        [JsonProperty("RequiredSocketType")] public string RequiredSocketType { get; set; }
        [JsonProperty("Modifiers")] public LegacyModifierDto[] Modifiers { get; set; }
    }
}
