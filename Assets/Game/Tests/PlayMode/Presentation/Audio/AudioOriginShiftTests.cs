#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Core.Events;
using Game.Data;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    public sealed class AudioOriginShiftTests
    {
        readonly List<Object> owned = new List<Object>();
        World world; AudioService service; AudioEventDrainSystem drain; Entity shift;
        AudioCueDefinition sfx, music; GameObject parent; AudioMixer mixer;
        int entries, resets;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        AudioSource Voice(string name) => parent.transform.Find("AudioRoot/" + name).GetComponent<AudioSource>();
        [UnitySetUp] public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
            entries = EventBus<StarSystemEntered>.SubscriberCount; resets = EventBus<SessionReset>.SubscriberCount;
            parent = Own(new GameObject("Audio origin test"));
            var config = Own(ScriptableObject.CreateInstance<AudioConfigDefinition>());
            var database = Own(ScriptableObject.CreateInstance<ContentDatabase>());
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Game/Audio/Mixer/GameMixer.mixer");
            sfx = Cue("cue_origin_sfx", AudioBus.Sfx, 1); music = Cue("cue_origin_music", AudioBus.Music, 0);
            using (var edit = new SerializedObject(config))
            {
                edit.FindProperty("mixer").objectReferenceValue = mixer;
                foreach (string bus in new[] { "Sfx", "Ui", "Music", "Ambience" })
                    edit.FindProperty(char.ToLowerInvariant(bus[0]) + bus.Substring(1) + "Group").objectReferenceValue = mixer.FindMatchingGroups("Master/" + bus)[0];
                edit.FindProperty("engineLoop").objectReferenceValue = sfx;
                edit.FindProperty("musicExploration").objectReferenceValue = music;
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            database.EditorSetDefinitions(new List<ContentDefinition> { sfx, music });
            service = new AudioService(config, database, AudioVolumes.Default, parent.transform);
            parent.GetComponentInChildren<AudioRoot>().enabled = false;
            world = new World("Audio origin test"); world.CreateSystem<SfxEventQueueCreateSystem>();
            shift = world.EntityManager.CreateEntity(typeof(OriginShiftRecord));
            service.BindTo(world); drain = world.GetExistingSystemManaged<AudioEventDrainSystem>(); drain.Update();
            Assert.AreEqual(entries + 1, EventBus<StarSystemEntered>.SubscriberCount);
            Assert.AreEqual(resets + 1, EventBus<SessionReset>.SubscriberCount);
        }
        AudioCueDefinition Cue(string id, AudioBus bus, float spatial)
        {
            var cue = Own(ScriptableObject.CreateInstance<AudioCueDefinition>());
            using var edit = new SerializedObject(cue);
            edit.FindProperty("id").stringValue = id; edit.FindProperty("bus").intValue = (int)bus;
            edit.FindProperty("spatialBlend").floatValue = spatial; edit.FindProperty("loop").boolValue = true;
            edit.FindProperty("volumeRandom").floatValue = 0; edit.FindProperty("pitchRandom").floatValue = 0;
            edit.FindProperty("cooldown").floatValue = 0;
            edit.FindProperty("maxDistance").floatValue = 4000;
            var clips = edit.FindProperty("clips"); clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = Own(AudioClip.Create(id, 480000, 1, 48000, false));
            edit.ApplyModifiedPropertiesWithoutUndo(); return cue;
        }
        void StartVoices()
        {
            service.PlayCueAt(sfx.Id, new float3(1990, 0, 0), 1);
            service.SetLoopTarget(0, 1, 1, new float3(1990, 0, 0));
            service.SetSceneMusicContext("World"); service.Update(2);
            Assert.NotNull(Voice("SfxVoice_00").clip); Assert.NotNull(Voice("LoopVoice_0").clip);
            Assert.NotNull(Voice("MusicA").clip);
        }
        void Shift(uint sequence, float x, byte changed = 0)
        {
            world.EntityManager.SetComponentData(shift, new OriginShiftRecord { Sequence = sequence, Delta = new float3(x,0,0), StarSystemChanged = changed });
            drain.Update();
        }
        void Stopped()
        {
            Assert.IsNull(Voice("SfxVoice_00").clip); Assert.IsNull(Voice("LoopVoice_0").clip);
            Assert.AreEqual(0, service.ActiveSfxCount); Assert.AreSame(music.Clips[0], Voice("MusicA").clip);
        }
        [UnityTest] public IEnumerator Shift_MovesPlayingVoicesSameFrame()
        {
            StartVoices(); yield return null;
            Assert.IsTrue(Voice("SfxVoice_00").isPlaying); Assert.IsTrue(Voice("LoopVoice_0").isPlaying);
            Shift(1, -2000);
            Assert.AreEqual(-10, Voice("SfxVoice_00").transform.position.x, .01f);
            Assert.AreEqual(-10, Voice("LoopVoice_0").transform.position.x, .01f);
            drain.Update(); Assert.AreEqual(-10, Voice("SfxVoice_00").transform.position.x, .01f, "Sequence applies once.");
            Assert.AreSame(music.Clips[0], Voice("MusicA").clip);
        }
        [UnityTest] public IEnumerator HugeShift_StopsSfx()
        {
            StartVoices(); yield return null; Shift(1, -20001); Stopped();
        }
        [UnityTest] public IEnumerator StarSystemEntered_StopsSfxKeepsMusic()
        {
            StartVoices(); yield return null;
            using var query = world.EntityManager.CreateEntityQuery(typeof(SfxEventQueue));
            var queue = query.GetSingleton<SfxEventQueue>(); queue.Access.CompleteAccess();
            queue.Queue.Enqueue(new SfxEvent { Cue = sfx.Id.Hash, Volume = 1 });
            EventBus<StarSystemEntered>.Publish(new StarSystemEntered(1, StarSystemEntryReason.Jump, 1));
            EventBus<StarSystemEntered>.Drain(); Stopped(); Assert.IsTrue(queue.Queue.IsEmpty());
            drain.Update(); Assert.AreEqual(0, service.ActiveSfxCount);
            StartVoices(); service.SetMusicOverride(music.Id, 0); service.Update(0);
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Loaded)); EventBus<SessionReset>.Drain();
            Assert.AreEqual(MusicState.Exploration, service.MusicState); Stopped();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            service?.Dispose(); service = null; world?.Dispose(); world = null;
            Assert.AreEqual(entries, EventBus<StarSystemEntered>.SubscriberCount);
            Assert.AreEqual(resets, EventBus<SessionReset>.SubscriberCount);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
            owned.Clear();
            if (mixer != null) foreach (string bus in new[] { "Master", "Sfx", "Ui", "Music", "Ambience" }) mixer.ClearFloat(bus + "Volume");
            yield return null;
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
        }
    }
}
#endif
