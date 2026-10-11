using System;
using Game.Core;
using Game.Core.Audio;
using Game.Data;
using Game.Data.Definitions;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Audio
{
    /// <summary>Implements SPEC-64 §6.1 signatures. AUD-001: every member is a no-op returning defaults, so SPEC-01 can construct it at M0;
    /// AUD-003 and later tasks add the behaviour.</summary>
    public sealed class AudioService : IAudioService, IDisposable
    {
        readonly AudioStats stats = new AudioStats();

        public AudioService(AudioConfigDefinition config, ContentDatabase content, AudioVolumes initialVolumes, Transform parent) { }
        public void ApplyVolumes(in AudioVolumes v) { }          // stored; applied to the mixer on the next Update
        public void BindPause(IPauseService pause) { }           // called by GameRoot after PauseService exists
        public void BindTo(World world) { }                      // binds AudioEventDrainSystem + AudioLoopBridgeSystem; creates/sets CombatAudioCues
        public void Unbind(World world) { }
        public void SetListenerTarget(Transform pivot) { }       // SPEC-62 camera rig, on enable
        public void ClearListenerTarget(Transform pivot) { }     // no-op unless pivot is the current target
        public float3 ListenerPosition => float3.zero;
        public AudioStats Stats => stats;
        public MusicState MusicState => MusicState.Silent;

        public void PlayUi(UiSound sound) { }
        public void PlayCue(ContentId cue) { }
        public void PlayCueAt(ContentId cue, float3 position, float volume) { }
        public void PlayStinger(ContentId cue) { }
        public void SetMusicOverride(ContentId cue, float fadeSeconds) { }
        public void ClearMusicOverride(float fadeSeconds) { }

        public void Dispose() { }                                // stops all voices, destroys AudioRoot, disposes native memory, unsubscribes
    }
}
