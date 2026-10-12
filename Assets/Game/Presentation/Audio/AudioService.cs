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
    /// <summary>SPEC-64 §5.2/§6.1: lifetime, listener, fixed voice pools, mixer settings and pause. Managed one-shots and stinger ducking; spatial allocation and music direction follow in AUD-006/008.</summary>
    public sealed class AudioService : IAudioService, IDisposable
    {
        readonly AudioStats stats = new AudioStats();
        readonly AudioConfigDefinition config;
        readonly AudioSource[] sfxVoices = new AudioSource[40];
        readonly VoiceInfo[] voiceInfo = new VoiceInfo[40];
        NativeHashMap<ulong, int> cueSlotByHash;
        NativeArray<SfxCueInfo> cueInfo;
        AudioCueDefinition[] cues;
        double[] lastUiStart;
        int[] lastClip;
        readonly int[] uiSlots = new int[11];
        UiVoicePool uiPool;
        AudioSource musicA, musicB, stinger;
        float duck = 1, musicBaseA = 1, musicBaseB = 1;
        int randomFrame = -1, playOrder;
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
                var uiVoices = new AudioSource[8];
                for (int i = 0; i < uiVoices.Length; i++) uiVoices[i] = Voice("UiVoice_" + i, ui, 0, true, false);
                uiPool = new UiVoicePool(uiVoices);
                musicA = Voice("MusicA", music, 0, true, false); musicB = Voice("MusicB", music, 0, true, false);
                stinger = Voice("Stinger", music, 0, true, false);
                Voice("Ambience", ambience, 0, false, true);
                for (int i = 0; i < 8; i++) Voice("LoopVoice_" + i, sfx, 1, false, true);
                int count = 0;
                if (content != null) foreach (var def in content.Definitions) if (def is AudioCueDefinition) count++;
                cueSlotByHash = new NativeHashMap<ulong, int>(Math.Max(1, count), Allocator.Persistent);
                cueInfo = new NativeArray<SfxCueInfo>(count + 1, Allocator.Persistent);
                cues = new AudioCueDefinition[count + 1]; lastUiStart = new double[count + 1]; lastClip = new int[count + 1];
                Array.Fill(lastUiStart, double.NegativeInfinity); Array.Fill(lastClip, -1);
                int slot = 1;
                if (content != null) foreach (var def in content.Definitions)
                    if (def is AudioCueDefinition cue)
                    {
                        cueSlotByHash.Add(cue.Id.Hash.Value, slot); cues[slot] = cue;
                        cueInfo[slot] = new SfxCueInfo { Slot = slot, Priority = checked((byte)cue.Priority), Volume = cue.Volume, SpatialBlend = cue.SpatialBlend,
                            MinDistance = cue.MinDistance, MaxDistance = cue.MaxDistance, Cooldown = cue.Cooldown,
                            CoalesceCellSize = cue.CoalesceCellSize, CoalesceGain = cue.CoalesceGain, Coalesce = cue.Coalesce,
                            MaxVoices = cue.MaxVoices, CombatSignal = cue.CombatSignal }; slot++;
                    }
                if (config != null) for (int i = 0; i < uiSlots.Length && i < config.UiSounds.Count; i++)
                    if (config.UiSounds[i] != null) cueSlotByHash.TryGetValue(config.UiSounds[i].Id.Hash.Value, out uiSlots[i]);
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
            UpdateDucking(dt);
            for (int i = 0; i < voiceInfo.Length; i++) if (voiceInfo[i].Active && !sfxVoices[i].isPlaying) voiceInfo[i].Active = false;
        }
        void OnSceneChanged(Scene previous, Scene next)
        {
            SceneContext = MusicContext.Silent;
            if (config != null) foreach (var entry in config.SceneMusic) if (entry.sceneName == next.name) { SceneContext = entry.context; break; }
        }

        public void PlayUi(UiSound sound)
        {
            if (disposed || config == null || (int)sound >= uiSlots.Length) return;
            PlayUiSlot(uiSlots[(int)sound], 1);
        }
        public void PlayCue(ContentId cue)
        {
            if (!Resolve(cue, out int slot)) return;
            if (cues[slot].Bus == AudioBus.Ui) PlayUiSlot(slot, 1);
            else PlayTwoDimensional(slot, 1);
        }
        public void PlayCueAt(ContentId cue, float3 position, float volume)
        {
            if (!Resolve(cue, out int slot) || !math.isfinite(volume) || volume <= .001f || !math.all(math.isfinite(position))) return;
            // AUD-006 owns spatial candidates and allocation. Until then, only explicitly 2D cues play here (Q15).
            if (cues[slot].SpatialBlend > 0) return;
            if (cues[slot].Bus == AudioBus.Ui) PlayUiSlot(slot, math.saturate(volume));
            else PlayTwoDimensional(slot, math.saturate(volume));
        }
        public void PlayStinger(ContentId cue)
        {
            if (!Resolve(cue, out int slot) || cues[slot].Clips.Count == 0) return;
            if (duck == 1) { musicBaseA = musicA.volume; musicBaseB = musicB.volume; }
            stinger.Stop(); StartOneShot(stinger, slot, 1, config.MusicGroup, true);
        }
        bool Resolve(ContentId id, out int slot)
        {
            slot = 0; if (disposed || config == null || id.IsNone) return false;
            if (cueSlotByHash.TryGetValue(id.Hash.Value, out slot)) return true;
            GameLog.WarnOnce("Audio.UnknownCue." + id.Value, "Audio", "Unknown cue: " + id.Value); return false;
        }
        void PlayUiSlot(int slot, float volume)
        {
            if (slot == 0 || cues[slot].Clips.Count == 0) return;
            double now = Time.unscaledTimeAsDouble;
            if (now - lastUiStart[slot] < cues[slot].Cooldown) return;
            StartOneShot(uiPool.Next(), slot, volume, config.UiGroup, true); lastUiStart[slot] = now;
        }
        void PlayTwoDimensional(int slot, float volume)
        {
            if (cues[slot].Clips.Count == 0) return;
            // Temporary managed-cue entry to the existing SFX pool. Same victim rules as §5.3; AUD-006 centralizes these.
            var cue = cues[slot]; float audibility = volume * cue.Volume;
            int same = 0, oldest = -1, free = -1, victim = -1;
            for (int i = 0; i < voiceInfo.Length; i++)
            {
                var info = voiceInfo[i];
                if (info.Active && !sfxVoices[i].isPlaying) { info.Active = false; voiceInfo[i] = info; }
                if (!info.Active) { if (free < 0) free = i; continue; }
                if (info.Slot == slot) { same++; if (oldest < 0 || info.StartTime < voiceInfo[oldest].StartTime) oldest = i; }
                if (victim < 0 || info.Priority > voiceInfo[victim].Priority ||
                    (info.Priority == voiceInfo[victim].Priority && (info.Audibility < voiceInfo[victim].Audibility ||
                    (info.Audibility == voiceInfo[victim].Audibility && info.StartTime < voiceInfo[victim].StartTime)))) victim = i;
            }
            int chosen;
            if (same >= cue.MaxVoices) { if (audibility <= voiceInfo[oldest].Audibility) return; chosen = oldest; }
            else if (free >= 0) chosen = free;
            else
            {
                if (victim < 0 || (voiceInfo[victim].Priority <= cue.Priority &&
                    (voiceInfo[victim].Priority != cue.Priority || voiceInfo[victim].Audibility >= audibility))) return;
                chosen = victim;
            }
            var bus = cue.Bus == AudioBus.Music ? config.MusicGroup : cue.Bus == AudioBus.Ambience ? config.AmbienceGroup : config.SfxGroup;
            var source = sfxVoices[chosen]; source.Stop(); source.transform.position = ListenerPosition;
            StartOneShot(source, slot, volume, bus, cue.Bus == AudioBus.Music);
            voiceInfo[chosen] = new VoiceInfo { Slot = slot, Priority = (byte)cue.Priority, Audibility = audibility,
                StartTime = Time.unscaledTimeAsDouble, Active = true };
        }
        void StartOneShot(AudioSource source, int slot, float volume, AudioMixerGroup bus, bool ignorePause)
        {
            var cue = cues[slot];
            if (randomFrame != Time.frameCount) { randomFrame = Time.frameCount; playOrder = 0; }
            uint seed = unchecked((uint)(Time.frameCount * 7919 + playOrder++ + 1));
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            int count = cue.Clips.Count, previous = lastClip[slot];
            int clip = random.NextInt(count > 1 && previous >= 0 ? count - 1 : count);
            if (count > 1 && previous >= 0 && clip >= previous) clip++;
            lastClip[slot] = clip;
            source.clip = cue.Clips[clip]; source.volume = math.saturate(cue.Volume * volume * (1 + random.NextFloat(-1,1) * cue.VolumeRandom));
            source.pitch = cue.Pitch * (1 + random.NextFloat(-1,1) * cue.PitchRandom);
            source.outputAudioMixerGroup = bus; source.spatialBlend = 0; source.loop = false; source.ignoreListenerPause = ignorePause;
            source.priority = cue.Priority; source.Play();
        }
        void UpdateDucking(float dt)
        {
            if (config == null) return;
            bool playing = stinger.isPlaying;
            if (!playing && duck == 1) return;
            float attenuated = Mathf.Pow(10, config.StingerDuckDb / 20);
            float target = playing ? attenuated : 1;
            float duration = playing ? config.StingerAttack : config.StingerRelease;
            duck = duration <= 0 ? target : Mathf.MoveTowards(duck, target, Mathf.Abs(1 - attenuated) * Mathf.Max(0,dt) / duration);
            // Always derive from unducked voice gains, never compound last frame's attenuation.
            musicA.volume = musicBaseA * duck; musicB.volume = musicBaseB * duck;
        }
        // AUD-008's director supplies the crossfade gains before ducking; these are not mixer settings.
        internal void SetMusicVoiceVolumes(float a, float b)
        { musicBaseA = a; musicBaseB = b; musicA.volume = a * duck; musicB.volume = b * duck; }

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
