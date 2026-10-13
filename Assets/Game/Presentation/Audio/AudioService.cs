using System;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Voxel.PresentationStreams;
using Game.Data;
using Game.Data.Definitions;
using Unity.Entities;
using Unity.Collections;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using Unity.Mathematics;
using UnityEngine;
using Unity.Profiling;

namespace Game.Presentation.Audio
{
    /// <summary>SPEC-64 §5.2/§6.1: lifetime, listener, fixed voice pools, mixer settings, pause and music direction.</summary>
    public sealed class AudioService : IAudioService, IDisposable
    {
        readonly AudioStats stats = new AudioStats();
        readonly AudioConfigDefinition config;
        readonly AudioSource[] sfxVoices = new AudioSource[40];
        readonly VoiceInfo[] voiceInfo = new VoiceInfo[40];
        readonly AudioSource[] loopVoices = new AudioSource[8];
        readonly int[] loopSlots = new int[8];
        readonly float[] loopVolumes = new float[8], loopTargets = new float[8];
        readonly bool[] loopActive = new bool[8];
        NativeHashMap<ulong, int> cueSlotByHash;
        NativeArray<SfxCueInfo> cueInfo;
        AudioCueDefinition[] cues;
        double[] lastUiStart;
        int[] lastClip;
        readonly int[] uiSlots = new int[11];
        UiVoicePool uiPool;
        AudioSource musicA, musicB, stinger;
        float duck = 1, musicBaseA = 1, musicBaseB = 1;
        static readonly ProfilerMarker UpdateMarker = new ProfilerMarker(ProfilerCategory.Scripts, "AudioService.Update");
        MusicDirector musicDirector;
        ContentHash musicOverride;
        float overrideFade, clearOverrideFade;
        bool overridePending, clearOverridePending, musicFading;
        int combatSignals, musicIncoming = -1;
        float musicFadeTime, musicFadeDuration, musicStartA, musicStartB, musicTarget;
        int randomFrame = -1, playOrder;
        AudioRoot root;
        Transform listener, listenerTarget;
        IPauseService pause;
        World boundWorld;
        AudioEventDrainSystem drainSystem;
        AudioLoopBridgeSystem loopSystem;
        internal NativeHashMap<ulong, int> CueSlots => cueSlotByHash;
        internal NativeArray<SfxCueInfo> CueInfo => cueInfo;
        internal float CombatRadius => config != null ? config.CombatRadius : 0;
        internal bool EngineLoopAvailable => LoopSlot(0) != 0;
        internal bool BeamLoopAvailable => LoopSlot(1) != 0;
        internal float BeamLoopRadius => config != null ? config.BeamLoopRadius : 0;
        internal float BeamLoopPitch => config != null && config.BeamLoop != null ? config.BeamLoop.Pitch : 1;
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
                for (int i = 0; i < loopVoices.Length; i++)
                { loopVoices[i] = Voice("LoopVoice_" + i, sfx, 1, false, true); loopVoices[i].volume = 0; }
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
                musicDirector = config != null ? new MusicDirector(Hash(config.MusicMenu), Hash(config.MusicExploration),
                    Hash(config.MusicCombat), Hash(config.MusicEditor), config.CombatWindowSeconds, config.CombatEnterSignals,
                    config.CombatHoldSeconds, config.FadeDefault, config.FadeToCombat, config.FadeFromCombat) :
                    new MusicDirector(ContentHash.None, ContentHash.None, ContentHash.None, ContentHash.None);
                ApplyVolumes(initialVolumes); OnSceneChanged(default, SceneManager.GetActiveScene());
                SceneManager.activeSceneChanged += OnSceneChanged;
                EventBus<SessionReset>.Subscribe(OnSessionReset);
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
        public void BindTo(World world)
        {
            if (disposed || world == null || !world.IsCreated) return;
            if (!ReferenceEquals(boundWorld, world))
            {
                if (boundWorld != null) Unbind(boundWorld);
                boundWorld = world; drainSystem = world.GetOrCreateSystemManaged<AudioEventDrainSystem>();
                drainSystem.Bind(this); stats.Bound = true;
                loopSystem = world.GetOrCreateSystemManaged<AudioLoopBridgeSystem>(); loopSystem.Bind(this);
            }
            // Boot-owned singleton survives scene teardown; rebind updates it without adding another entity.
            var cues = new CombatAudioCues {
                ImpactKinetic = Hash(config != null ? config.ImpactKinetic : null), ImpactEnergy = Hash(config != null ? config.ImpactEnergy : null),
                ImpactExplosive = Hash(config != null ? config.ImpactExplosive : null), ImpactRam = Hash(config != null ? config.ImpactRam : null),
                ExplosionSmall = Hash(config != null ? config.ExplosionSmall : null), ExplosionLarge = Hash(config != null ? config.ExplosionLarge : null),
                ShieldHit = Hash(config != null ? config.ShieldHit : null), CellBreak = Hash(config != null ? config.CellBreak : null),
                BreakOff = Hash(config != null ? config.BreakOff : null), ShipDeath = Hash(config != null ? config.ShipDeath : null) };
            var em = world.EntityManager;
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<CombatAudioCues>());
            if (query.IsEmpty)
            {
                var entity = em.CreateEntity(typeof(CombatAudioCues)); em.SetName(entity, "CombatAudioCues"); em.SetComponentData(entity, cues);
            }
            else query.SetSingleton(cues);
        }
        public void Unbind(World world)
        {
            if (!ReferenceEquals(boundWorld, world)) return;
            if (boundWorld != null && boundWorld.IsCreated && drainSystem != null) drainSystem.Bind(null);
            if (boundWorld != null && boundWorld.IsCreated && loopSystem != null) loopSystem.Bind(null);
            StopLoopVoices(); loopSystem = null;
            boundWorld = null; drainSystem = null; stats.Bound = false;
        }
        public void SetListenerTarget(Transform pivot) { if (!disposed) listenerTarget = pivot; }
        public void ClearListenerTarget(Transform pivot) { if (listenerTarget == pivot) listenerTarget = null; }
        public float3 ListenerPosition => listener != null ? (float3)listener.position : float3.zero;
        public AudioStats Stats => stats;
        public MusicState MusicState => musicDirector != null ? musicDirector.State : MusicState.Silent;
        internal void Update(float dt)
        {
            if (disposed) return;
            using var marker = UpdateMarker.Auto();
            dt = float.IsFinite(dt) ? math.max(0, dt) : 0;
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
            UpdateMusic(dt);
            UpdateDucking(dt);
            UpdateLoopVoices(dt);
            for (int i = 0; i < voiceInfo.Length; i++) if (voiceInfo[i].Active && !sfxVoices[i].isPlaying) voiceInfo[i].Active = false;
        }
        void OnSceneChanged(Scene previous, Scene next)
            => SetSceneMusicContext(next.name);

        internal void SetSceneMusicContext(string sceneName)
        {
            if (disposed) return;
            SceneContext = MusicContext.Silent;
            if (config != null) foreach (var entry in config.SceneMusic) if (entry.sceneName == sceneName) { SceneContext = entry.context; break; }
        }

        static ContentHash Hash(AudioCueDefinition cue) => cue != null ? cue.Id.Hash : ContentHash.None;
        internal void ReportCombatSignals(int count)
        {
            if (disposed || count <= 0) return;
            combatSignals += math.min(count, int.MaxValue - combatSignals);
        }
        void UpdateMusic(float dt)
        {
            var previous = musicDirector.State;
            var command = musicDirector.Update(SceneContext, combatSignals, !musicOverride.IsNone, musicOverride, overrideFade, dt);
            combatSignals = 0;
            bool replaceOverride = command.Target == MusicState.Override && overridePending;
            if (command.Change || replaceOverride)
            {
                if (clearOverridePending && previous == MusicState.Override && command.Target != MusicState.Silent)
                    command.Fade = clearOverrideFade;
                ApplyMusicCommand(command);
            }
            if (command.Target == MusicState.Override) overridePending = false;
            clearOverridePending = false;
            AdvanceMusicFade(dt);
            stats.Music = musicDirector.State;
        }
        void ApplyMusicCommand(in MusicCommand command)
        {
            musicStartA = musicA.clip != null ? musicBaseA : 0;
            musicStartB = musicB.clip != null ? musicBaseB : 0;
            musicFadeTime = 0; musicFadeDuration = command.Fade; musicIncoming = -1; musicTarget = 0;
            if (!command.Cue.IsNone && cueSlotByHash.TryGetValue(command.Cue.Value, out int slot) && cues[slot].Clips.Count > 0)
            {
                // Two voices cannot retain three clips. Replace the quieter one, preserving both unducked gains (Q21).
                musicIncoming = musicStartA <= musicStartB ? 0 : 1;
                var incoming = musicIncoming == 0 ? musicA : musicB;
                incoming.Stop(); StartOneShot(incoming, slot, 1, config.MusicGroup, true);
                incoming.loop = cues[slot].Loop;
                musicTarget = incoming.volume;
            }
            musicFading = musicFadeDuration > 0 && (musicIncoming >= 0 || musicStartA > 0 || musicStartB > 0);
            SetMusicVoiceVolumes(musicStartA, musicStartB);
            if (!musicFading) FinishMusicFade();
        }
        void AdvanceMusicFade(float dt)
        {
            if (!musicFading) return;
            musicFadeTime = math.min(musicFadeDuration, musicFadeTime + dt);
            float t = musicFadeTime / musicFadeDuration;
            float cosine = math.cos(t * math.PI * .5f), sine = math.sin(t * math.PI * .5f);
            float a = musicStartA * cosine, b = musicStartB * cosine;
            // Squared-gain interpolation reduces to sin/cos from a steady track and remains continuous on interruption.
            if (musicIncoming == 0) a = math.sqrt(a * a + musicTarget * musicTarget * sine * sine);
            if (musicIncoming == 1) b = math.sqrt(b * b + musicTarget * musicTarget * sine * sine);
            SetMusicVoiceVolumes(a, b);
            if (musicFadeTime >= musicFadeDuration) FinishMusicFade();
        }
        void FinishMusicFade()
        {
            musicFading = false;
            if (musicIncoming != 0) { musicA.Stop(); musicA.clip = null; }
            if (musicIncoming != 1) { musicB.Stop(); musicB.clip = null; }
            SetMusicVoiceVolumes(musicIncoming == 0 ? musicTarget : 0, musicIncoming == 1 ? musicTarget : 0);
        }
        void OnSessionReset(SessionReset reset)
        {
            if (disposed || musicDirector == null) return;
            musicOverride = ContentHash.None; overridePending = clearOverridePending = false; combatSignals = 0;
            musicFading = false; musicA.Stop(); musicB.Stop(); stinger.Stop(); musicA.clip = musicB.clip = null;
            duck = 1; SetMusicVoiceVolumes(0, 0); musicDirector.Reset();
            // Reset to the current mapped context immediately, with empty combat history and no scripted override.
            var command = musicDirector.Update(SceneContext, 0, false, ContentHash.None, 0, 0);
            if (command.Change) ApplyMusicCommand(command);
            stats.Music = musicDirector.State;
            // AUD-012 owns the complementary SFX/loop clear; AUD-008 resets music only.
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
            var info = cueInfo[slot];
            float distance = math.distance(position, ListenerPosition);
            if (info.SpatialBlend > 0 && distance > info.MaxDistance) return;
            var candidate = new SfxCandidate { Slot = slot, Pos = position, FinalVolume = math.saturate(volume), Count = 1 };
            candidate.Audibility = candidate.FinalVolume * info.Volume *
                (info.SpatialBlend > 0 ? math.saturate(info.MinDistance / math.max(distance, info.MinDistance)) : 1);
            RefreshSfxVoices();
            TryStartSfx(candidate, Time.unscaledTimeAsDouble, false);
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
            RefreshSfxVoices();
            var candidate = new SfxCandidate { Slot = slot, Pos = ListenerPosition, FinalVolume = volume,
                Audibility = volume * cueInfo[slot].Volume, Flags = SfxEventFlags.PlayerSource, Count = 1 };
            TryStartSfx(candidate, Time.unscaledTimeAsDouble, true);
        }
        internal void RefreshSfxVoices()
        {
            for (int i = 0; i < voiceInfo.Length; i++)
                if (voiceInfo[i].Active && !sfxVoices[i].isPlaying) voiceInfo[i].Active = false;
        }
        internal bool TryStartSfx(in SfxCandidate candidate, double now, bool forceTwoDimensional = false)
        {
            if (disposed || config == null || candidate.Slot <= 0 || candidate.Slot >= cues.Length ||
                cues[candidate.Slot].Clips.Count == 0) return false;
            int chosen = SfxVoiceAllocator.Select(candidate, voiceInfo, cueInfo[candidate.Slot], out bool stolen);
            if (chosen < 0) { stats.RejectedNoVoice++; return false; }
            StartSfxVoice(chosen, candidate, now, forceTwoDimensional);
            stats.Started++; if (stolen) stats.Stolen++;
            return true;
        }
        internal void StartSfxVoice(int index, in SfxCandidate candidate, double now, bool forceTwoDimensional = false)
        {
            int slot = candidate.Slot; var cue = cues[slot]; var source = sfxVoices[index];
            var bus = cue.Bus == AudioBus.Music ? config.MusicGroup : cue.Bus == AudioBus.Ambience ? config.AmbienceGroup :
                cue.Bus == AudioBus.Ui ? config.UiGroup : config.SfxGroup;
            source.Stop(); source.transform.position = candidate.Pos;
            StartOneShot(source, slot, candidate.FinalVolume, bus, cue.Bus == AudioBus.Music || cue.Bus == AudioBus.Ui,
                forceTwoDimensional ? 0 : cue.SpatialBlend, candidate.Order);
            voiceInfo[index] = new VoiceInfo { Slot = slot, Priority = (byte)cue.Priority, Audibility = candidate.Audibility,
                StartTime = now, Active = true };
        }
        internal int ActiveSfxCount
        {
            get { int count = 0; for (int i = 0; i < voiceInfo.Length; i++) if (voiceInfo[i].Active) count++; return count; }
        }
        // AUD-009: normalized loop envelopes; mixer settings stay on the mixer and cue gain is applied once.
        int LoopSlot(int index)
        {
            if (disposed || config == null || !cueSlotByHash.IsCreated) return 0;
            var cue = index == 0 ? config.EngineLoop : config.BeamLoop;
            return cue != null && cueSlotByHash.TryGetValue(cue.Id.Hash.Value, out int slot) && cues[slot].Clips.Count > 0 ? slot : 0;
        }
        internal void SetLoopTarget(int index, float volume, float pitch, float3 position)
        {
            if (disposed || (uint)index >= 7) return; // Voice 7 is spare.
            int slot = LoopSlot(index);
            if (slot == 0 || !math.all(math.isfinite(position))) { StopLoopVoice(index); return; }
            volume = math.isfinite(volume) ? math.saturate(volume) : 0;
            if (loopSlots[index] != slot) StopLoopVoice(index);
            var source = loopVoices[index]; source.transform.position = position;
            source.pitch = math.isfinite(pitch) ? math.clamp(pitch, .1f, 3) : 1;
            loopSlots[index] = slot; loopTargets[index] = volume;
            if (!loopActive[index] && volume > 0)
            {
                var cue = cues[slot];
                var random = new Unity.Mathematics.Random(unchecked((uint)Time.frameCount * 7919u + (uint)index + 1u) | 1u);
                int count = cue.Clips.Count, previous = lastClip[slot];
                int clip = random.NextInt(count > 1 && previous >= 0 ? count - 1 : count);
                if (count > 1 && previous >= 0 && clip >= previous) clip++;
                lastClip[slot] = clip; source.clip = cue.Clips[clip]; source.volume = 0;
                source.spatialBlend = 1; // Keep the construction-time Sfx routing, including its specified Master fallback.
                source.minDistance = cue.MinDistance; source.maxDistance = cue.MaxDistance; source.priority = cue.Priority;
                source.ignoreListenerPause = false; source.loop = true; source.Play(); loopActive[index] = true;
            }
            stats.ActiveLoopVoices = ActiveLoopCount;
        }
        internal void FadeOutLoopVoice(int index)
        {
            if (disposed || (uint)index >= 7) return;
            if (LoopSlot(index) == 0) StopLoopVoice(index);
            else { loopTargets[index] = 0; if (index == 0) loopVoices[index].pitch = .8f; }
        }
        internal void StopLoopVoice(int index)
        {
            if ((uint)index >= loopVoices.Length) return;
            if (loopVoices[index] != null) { loopVoices[index].Stop(); loopVoices[index].clip = null; loopVoices[index].volume = 0; }
            loopSlots[index] = 0; loopVolumes[index] = loopTargets[index] = 0; loopActive[index] = false;
            stats.ActiveLoopVoices = ActiveLoopCount;
        }
        internal void StopLoopVoices()
        { for (int i = 0; i < loopVoices.Length; i++) StopLoopVoice(i); }
        internal int ActiveLoopCount
        { get { int count = 0; for (int i = 0; i < loopActive.Length; i++) if (loopActive[i]) count++; return count; } }
        void UpdateLoopVoices(float dt)
        {
            float fade = config != null ? config.LoopFadeSeconds : 0;
            float step = !math.isfinite(fade) || fade <= 0 ? 1 : (math.isfinite(dt) ? math.max(0, dt) / fade : 0);
            for (int i = 0; i < loopVoices.Length; i++)
            {
                if (!loopActive[i]) continue;
                if (LoopSlot(i) == 0) { StopLoopVoice(i); continue; }
                loopVolumes[i] = Mathf.MoveTowards(loopVolumes[i], loopTargets[i], step);
                loopVoices[i].volume = math.saturate(cues[loopSlots[i]].Volume * loopVolumes[i]);
                if (loopTargets[i] == 0 && loopVolumes[i] == 0) StopLoopVoice(i);
            }
            stats.ActiveLoopVoices = ActiveLoopCount;
        }
        void StartOneShot(AudioSource source, int slot, float volume, AudioMixerGroup bus, bool ignorePause, float spatial = 0, int order = -1)
        {
            var cue = cues[slot];
            if (randomFrame != Time.frameCount) { randomFrame = Time.frameCount; playOrder = 0; }
            uint seed = unchecked((uint)(Time.frameCount * 7919 + (order >= 0 ? order : playOrder++) + 1));
            var random = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            int count = cue.Clips.Count, previous = lastClip[slot];
            int clip = random.NextInt(count > 1 && previous >= 0 ? count - 1 : count);
            if (count > 1 && previous >= 0 && clip >= previous) clip++;
            lastClip[slot] = clip;
            source.clip = cue.Clips[clip]; source.volume = math.saturate(cue.Volume * volume * (1 + random.NextFloat(-1,1) * cue.VolumeRandom));
            source.pitch = cue.Pitch * (1 + random.NextFloat(-1,1) * cue.PitchRandom);
            source.outputAudioMixerGroup = bus; source.spatialBlend = spatial;
            source.minDistance = cue.MinDistance; source.maxDistance = cue.MaxDistance; source.loop = false; source.ignoreListenerPause = ignorePause;
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

        public void SetMusicOverride(ContentId cue, float fadeSeconds)
        {
            if (disposed) return;
            if (!float.IsFinite(fadeSeconds) || fadeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(fadeSeconds));
            if (!Resolve(cue, out int slot) || cues[slot].Clips.Count == 0) return;
            musicOverride = cues[slot].Id.Hash; overrideFade = fadeSeconds; overridePending = true; clearOverridePending = false;
        }
        public void ClearMusicOverride(float fadeSeconds)
        {
            if (disposed) return;
            if (!float.IsFinite(fadeSeconds) || fadeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(fadeSeconds));
            musicOverride = ContentHash.None; overridePending = false; clearOverridePending = true; clearOverrideFade = fadeSeconds;
        }

        public void Dispose()
        {
            if (disposed) return; Unbind(boundWorld); disposed = true;
            SceneManager.activeSceneChanged -= OnSceneChanged; AudioListener.pause = false;
            EventBus<SessionReset>.Unsubscribe(OnSessionReset);
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
