using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>BlockItem</c> (SPEC-72 §4.3).</summary>
    public class LegacyBlockItemDto : LegacyItemDto
    {
        [JsonProperty("BlockMesh")] public LegacyRef BlockMesh { get; set; }
        [JsonProperty("Health")] public double Health { get; set; }
        [JsonProperty("BlockSize")] public int[] BlockSize { get; set; }
        [JsonProperty("VoxelShape")] public int[][] VoxelShape { get; set; }
        [JsonProperty("RotationLocked")] public bool RotationLocked { get; set; }
        [JsonProperty("SocketTypeProvided")] public string SocketTypeProvided { get; set; }
        [JsonProperty("SocketsProvidedCount")] public int SocketsProvidedCount { get; set; }
        [JsonProperty("EnergyRegenRate")] public double EnergyRegenRate { get; set; }
        [JsonProperty("MaxEnergyStorage")] public double MaxEnergyStorage { get; set; }
        [JsonProperty("PassiveDrainRate")] public double PassiveDrainRate { get; set; }
        [JsonProperty("ClearanceDirections")] public int[][] ClearanceDirections { get; set; }
        [JsonProperty("Modifiers")] public LegacyModifierDto[] Modifiers { get; set; }
    }
}
