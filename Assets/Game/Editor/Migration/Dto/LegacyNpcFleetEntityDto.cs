using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>NPCFleetEntity</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyNpcFleetEntityDto : LegacySystemEntityDto
    {
        [JsonProperty("allegiance")] public string Allegiance { get; set; }
        [JsonProperty("detectionRadius")] public double DetectionRadius { get; set; }
        [JsonProperty("patrolRadius")] public double PatrolRadius { get; set; }
        [JsonProperty("chasesPlayer")] public bool ChasesPlayer { get; set; }
        [JsonProperty("movementSpeed")] public double MovementSpeed { get; set; }
        [JsonProperty("useFactionDoctrine")] public bool UseFactionDoctrine { get; set; }
        [JsonProperty("doctrineMinShips")] public int DoctrineMinShips { get; set; }
        [JsonProperty("doctrineMaxShips")] public int DoctrineMaxShips { get; set; }
    }
}
