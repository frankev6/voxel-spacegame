using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>MirrorBlockItem</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyMirrorBlockItemDto : LegacyWeaponBlockItemDto
    {
        [JsonProperty("BeamDamageMultiplier")] public double BeamDamageMultiplier { get; set; }
        [JsonProperty("BeamThickness")] public double BeamThickness { get; set; }
    }
}
