using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>Projectile</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyProjectileDto : LegacyDto
    {
        [JsonProperty("VisualType")] public string VisualType { get; set; }
        [JsonProperty("DamageType")] public string DamageType { get; set; }
        [JsonProperty("Damage")] public double Damage { get; set; }
        [JsonProperty("ExplosionRadius")] public double ExplosionRadius { get; set; }
        [JsonProperty("Size")] public double Size { get; set; }
        [JsonProperty("Speed")] public double Speed { get; set; }
        [JsonProperty("LifetimeMin")] public double LifetimeMin { get; set; }
        [JsonProperty("LifetimeMax")] public double LifetimeMax { get; set; }
        [JsonProperty("IsHoming")] public bool IsHoming { get; set; }
        [JsonProperty("HomingTurnRate")] public double HomingTurnRate { get; set; }
        [JsonProperty("ExplodesOnDeath")] public bool ExplodesOnDeath { get; set; }
    }
}
