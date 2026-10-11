using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>CombatEncounter</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyCombatEncounterDto : LegacyDto
    {
        [JsonProperty("encounterId")] public string EncounterId { get; set; }
        [JsonProperty("isUnique")] public bool IsUnique { get; set; }
        [JsonProperty("repeatable")] public bool Repeatable { get; set; }
        [JsonProperty("possibleFormations")] public LegacyCombatEncounterDto.Formation[] PossibleFormations { get; set; }
        public sealed class Formation { [JsonProperty("ships")] public LegacyRef[] Ships { get; set; } }
    }
}
