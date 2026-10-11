using Game.Data.Validation;
using UnityEngine;

namespace Game.Data.Definitions
{
    /// <summary>One playable sound (SPEC-64 §4.3). Content id prefix "cue_" (e.g. cue_weapon_laser_fire).</summary>
    [ContentIdPrefix("cue_")]
    [CreateAssetMenu(menuName = "Game/Content/Audio Cue", fileName = "cue_new")]
    public sealed class AudioCueDefinition : ContentDefinition
    {
        [SerializeField] AudioClip[] clips = new AudioClip[0];        // >= 1; never the same clip twice in a row when >= 2
        [SerializeField] AudioBus bus = AudioBus.Sfx;
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField, Range(0f, 0.5f)] float volumeRandom = 0.1f;   // ± fraction
        [SerializeField, Range(0.1f, 3f)] float pitch = 1f;
        [SerializeField, Range(0f, 0.5f)] float pitchRandom = 0.05f;   // ± fraction
        [SerializeField, Range(1, 32)] int maxVoices = 4;
        [SerializeField, Range(0, 255)] int priority = 128;            // 0 = most important
        [SerializeField, Range(0f, 1f)] float spatialBlend = 1f;       // 0 = 2D, 1 = 3D
        [SerializeField, Min(0.1f)] float minDistance = 30f;           // metres, logarithmic rolloff
        [SerializeField, Min(1f)] float maxDistance = 600f;            // also the distance-cull radius
        [SerializeField, Min(0f)] float cooldown = 0.05f;              // seconds between starts in the same coalescing group
        [SerializeField] bool loop;                                    // music, ambience, loop voices only
        [SerializeField] CoalesceMode coalesce = CoalesceMode.PerCell;
        [SerializeField, Min(1f)] float coalesceCellSize = 16f;
        [SerializeField, Range(0f, 1f)] float coalesceGain = 0.25f;
        [SerializeField] bool combatSignal;                            // counts toward combat music

        public System.Collections.Generic.IReadOnlyList<AudioClip> Clips => clips;
        public AudioBus Bus => bus;
        public float Volume => volume;
        public float VolumeRandom => volumeRandom;
        public float Pitch => pitch;
        public float PitchRandom => pitchRandom;
        public int MaxVoices => maxVoices;
        public int Priority => priority;
        public float SpatialBlend => spatialBlend;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;
        public float Cooldown => cooldown;
        public bool Loop => loop;
        public CoalesceMode Coalesce => coalesce;
        public float CoalesceCellSize => coalesceCellSize;
        public float CoalesceGain => coalesceGain;
        public bool CombatSignal => combatSignal;

        /// <summary>AUD-E001 clips empty or containing null; AUD-E002 minDistance ≥ maxDistance; AUD-E003 Ui bus with spatialBlend &gt; 0.
        /// Music/ambience loop rules are checked by AudioConfigDefinition (SPEC-64 open question 12).</summary>
        public override void Validate(ValidationScope scope, ContentValidationContext context)
        {
            base.Validate(scope, context);
            if (clips == null || clips.Length == 0) scope.Error("AUD-E001", nameof(clips), "A cue needs at least one clip.");
            else for (int i = 0; i < clips.Length; i++) if (clips[i] == null) scope.Error("AUD-E001", $"{nameof(clips)}[{i}]", "Clip entry is empty.");
            if (minDistance >= maxDistance) scope.Error("AUD-E002", nameof(minDistance), $"Min distance {minDistance} must be below max distance {maxDistance}.");
            if (bus == AudioBus.Ui && spatialBlend > 0f) scope.Error("AUD-E003", nameof(spatialBlend), "UI cues are 2D: spatial blend must be 0.");
        }
    }
}
