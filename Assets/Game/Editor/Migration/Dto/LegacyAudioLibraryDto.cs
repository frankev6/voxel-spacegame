using Newtonsoft.Json;

namespace Game.Editor.Migration.Dto
{
    /// <summary>Legacy <c>AudioLibrary</c> (SPEC-72 §4.3).</summary>
    public sealed class LegacyAudioLibraryDto : LegacyDto
    {
        [JsonProperty("Sounds")] public LegacyAudioLibraryDto.SoundEntry[] Sounds { get; set; }
        [JsonProperty("Music")] public LegacyAudioLibraryDto.MusicEntry[] Music { get; set; }
        public sealed class SoundEntry { [JsonProperty("ID")] public string Id { get; set; } [JsonProperty("Clip")] public LegacyRef Clip { get; set; } [JsonProperty("Volume")] public double Volume { get; set; } }
        public sealed class MusicEntry { [JsonProperty("Track")] public string Track { get; set; } [JsonProperty("Clip")] public LegacyRef Clip { get; set; } [JsonProperty("Volume")] public double Volume { get; set; } }
    }
}
