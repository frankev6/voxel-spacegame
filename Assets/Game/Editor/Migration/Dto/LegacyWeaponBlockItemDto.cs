using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>WeaponBlockItem</c> (SPEC-72 §4.3).</summary>
    public class LegacyWeaponBlockItemDto : LegacyBlockItemDto
    {
        [JsonProperty("ProjectileData")] public LegacyRef ProjectileData { get; set; }
        [JsonProperty("BaseAccuracy")] public double BaseAccuracy { get; set; }
        [JsonProperty("FireRate")] public double FireRate { get; set; }
        [JsonProperty("EnergyCost")] public double EnergyCost { get; set; }
        [JsonProperty("BurstCount")] public int BurstCount { get; set; }
        [JsonProperty("BurstDelay")] public double BurstDelay { get; set; }
        [JsonProperty("disableInputTrigger")] public bool DisableInputTrigger { get; set; }
        [JsonProperty("IsRotating")] public bool IsRotating { get; set; }
        [JsonProperty("RotationSpeed")] public double RotationSpeed { get; set; }
        [JsonProperty("MaxRotationAngle")] public double MaxRotationAngle { get; set; }
        [JsonProperty("MuzzlePoint")] public double[] MuzzlePoint { get; set; }
        [JsonProperty("MuzzleSoundID")] public string MuzzleSoundID { get; set; }
    }
}
