using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>PaintBucketItem</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyPaintBucketItemDto : LegacyItemDto
    {
        [JsonProperty("PaletteIndex")] public int PaletteIndex { get; set; }
    }
}
