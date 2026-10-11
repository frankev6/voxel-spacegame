using System;
using Game.Core;
using Game.Core.Audio;
using Game.Data;
using Game.Data.Definitions;
using Unity.Entities;
using Unity.Collections;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.2/§6.1: lifetime, listener, fixed voice pools, mixer settings and pause. Playback is added by AUD-005 onward.</summary>
    public sealed class AudioService : IAudioService, IDisposable
    {
        readonly AudioStats stats = new AudioStats();
        readonly AudioConfigDefinition config;
        readonly AudioSource[] sfxVoices = new AudioSource[40];
        readonly VoiceInfo[] voiceInfo = new VoiceInfo[40];
        NativeHashMap<ulong, int> cueSlotByHash;
        NativeArray<SfxCueInfo> cueInfo;
        AudioRoot root;
        Transform listener, listenerTarget;
        IPauseService pause;
        AudioVolumes pendingVolumes;
        bool volumesPending, disposed;
        internal MusicContext SceneContext { get; private set; }

        public AudioService(AudioConfigDefinition config, ContentDatabase content, AudioVolumes initialVolumes, Transform parent)
        {
            this.config = config;
            try
            {
                // FND-002's prefab already has an empty AudioRoot staging child. Adopt only that
                // empty placeholder; never overwrite an existing service or another component.
                var placeholder = parent != null ? parent.Find("AudioRoot") : null;
                if (placeholder != null && (placeholder.childCount != 0 || placeholder.GetComponents<Component>().Length != 1))
                    throw new InvalidOperationException("AudioRoot is already in use.");
                var go = placeholder != null ? placeholder.gameObject : new GameObject("AudioRoot"); go.transform.SetParent(parent, false);
                root = go.AddComponent<AudioRoot>(); root.Service = this;
                var ear = new GameObject("AudioListener"); ear.transform.SetParent(go.transform, false);
                ear.AddComponent<AudioListener>(); listener = ear.transform;
                var sfx = Group(config != null ? config.SfxGroup : null, "Sfx");
                var ui = Group(config != null ? config.UiGroup : null, "Ui");
                var music = Group(config != null ? config.MusicGroup : null, "Music");
                var ambience = Group(config != null ? config.AmbienceGroup : null, "Ambience");
                for (int i = 0; i < sfxVoices.Length; i++) sfxVoices[i] = Voice("SfxVoice_" + i.ToString("00"), sfx, 1, false, false);
                for (int i = 0; i < 8; i++) Voice("UiVoice_" + i, ui, 0, true, false);
                Voice("MusicA", music, 0, true, false); Voice("MusicB", music, 0, true, false); Voice("Stinger", music, 0, true, false);
                Voice("Ambience", ambience, 0, false, true);
                for (int i = 0; i < 8; i++) Voice("LoopVoice_" + i, sfx, 1, false, true);
                int count = 0;
                if (content != null) foreach (var def in content.Definitions) if (def is AudioCueDefinition) count++;
                cueSlotByHash = new NativeHashMap<ulong, int>(Math.Max(1, count), Allocator.Persistent);
                cueInfo = new NativeArray<SfxCueInfo>(count + 1, Allocator.Persistent);
                int slot = 1;
                if (content != null) foreach (var def in content.Definitions)
                    if (def is AudioCueDefinition cue)
                    {
                        cueSlotByHash.Add(cue.Id.Hash.Value, slot);
                        cueInfo[slot] = new SfxCueInfo { Slot = slot, Priority = checked((byte)cue.Priority), Volume = cue.Volume, SpatialBlend = cue.SpatialBlend,
                            MinDistance = cue.MinDistance, MaxDistance = cue.MaxDistance, Cooldown = cue.Cooldown,
                            CoalesceCellSize = cue.CoalesceCellSize, CoalesceGain = cue.CoalesceGain, Coalesce = cue.Coalesce,
                            MaxVoices = cue.MaxVoices, CombatSignal = cue.CombatSignal }; slot++;
                    }
                ApplyVolumes(initialVolumes); OnSceneChanged(default, SceneManager.GetActiveScene());
                SceneManager.activeSceneChanged += OnSceneChanged;
            }
            catch { Dispose(); throw; }
        }
        AudioMixerGroup Group(AudioMixerGroup group, string bus)
        {
            // M0 has no config/cues until AUD-007. Its listener and pools run silently (Q14).
            if (group != null || config == null) return group;
            GameLog.Error("Audio", "Missing " + bus + " mixer group; routing to Master.");
            if (config.Mixer != null) foreach (var candidate in config.Mixer.FindMatchingGroups("")) if (candidate.name == "Master") return candidate;
            return null;
        }
        AudioSource Voice(string name, AudioMixerGroup group, float spatial, bool ignorePause, bool loop)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.outputAudioMixerGroup = group;
            source.spatialBlend = spatial; source.rolloffMode = AudioRolloffMode.Logarithmic; source.ignoreListenerPause = ignorePause; source.loop = loop;
            return source;
        }
        public void ApplyVolumes(in AudioVolumes v) { if (disposed) return; pendingVolumes = v; volumesPending = true; }
        public void BindPause(IPauseService value) { if (!disposed) pause = value; }
        public void BindTo(World world) { }                      // AUD-006/007: system binding and CombatAudioCues
        public void Unbind(World world) { }
        public void SetListenerTarget(Transform pivot) { if (!disposed) listenerTarget = pivot; }
        public void ClearListenerTarget(Transform pivot) { if (listenerTarget == pivot) listenerTarget = null; }
        public float3 ListenerPosition => listener != null ? (float3)listener.position : float3.zero;
        public AudioStats Stats => stats;
        public MusicState MusicState => MusicState.Silent;       // AUD-008: music director
        internal void Update(float dt)
        {
            if (disposed) return;
            if (volumesPending)
            {
                var mixer = config != null ? config.Mixer : null;
                if (mixer != null)
                {
                    mixer.SetFloat("MasterVolume", VolumeMath.ToDb(pendingVolumes.Master)); mixer.SetFloat("MusicVolume", VolumeMath.ToDb(pendingVolumes.Music));
                    mixer.SetFloat("SfxVolume", VolumeMath.ToDb(pendingVolumes.Sfx)); mixer.SetFloat("UiVolume", VolumeMath.ToDb(pendingVolumes.Ui));
                    mixer.SetFloat("AmbienceVolume", VolumeMath.ToDb(pendingVolumes.Ambience));
                }
                volumesPending = false;
            }
            if (listenerTarget != null) listener.SetPositionAndRotation(listenerTarget.position, listenerTarget.rotation);
            else listenerTarget = null;
            AudioListener.pause = pause != null && pause.IsPaused;
            for (int i = 0; i < voiceInfo.Length; i++) if (voiceInfo[i].Active && !sfxVoices[i].isPlaying) voiceInfo[i].Active = false;
        }
        void OnSceneChanged(Scene previous, Scene next)
        {
            SceneContext = MusicContext.Silent;
            if (config != null) foreach (var entry in config.SceneMusic) if (entry.sceneName == next.name) { SceneContext = entry.context; break; }
        }

        public void PlayUi(UiSound sound) { }
        public void PlayCue(ContentId cue) { }
        public void PlayCueAt(ContentId cue, float3 position, float volume) { }
        public void PlayStinger(ContentId cue) { }
        public void SetMusicOverride(ContentId cue, float fadeSeconds) { }
        public void ClearMusicOverride(float fadeSeconds) { }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            SceneManager.activeSceneChanged -= OnSceneChanged; AudioListener.pause = false;
            listenerTarget = null; pause = null;
            if (cueInfo.IsCreated) cueInfo.Dispose(); if (cueSlotByHash.IsCreated) cueSlotByHash.Dispose();
            if (root != null)
            {
                root.Service = null;
                foreach (var source in root.GetComponentsInChildren<AudioSource>()) source.Stop();
                root.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject); else UnityEngine.Object.DestroyImmediate(root.gameObject);
            }
            root = null; listener = null;
        }
    }
}
