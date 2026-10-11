using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>ShipTableSO</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyShipTableDto : LegacyDto
    {
        [JsonProperty("ships")] public LegacyRef[] Ships { get; set; }
    }
}
