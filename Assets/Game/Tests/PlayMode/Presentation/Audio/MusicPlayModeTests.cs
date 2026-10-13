#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core.Audio;
using Game.Core.Events;
using Game.Data;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    /// <summary>AUD-008's required source-level crossfade check, using synthetic silent clips and simulated service time.</summary>
    public sealed class MusicPlayModeTests
    {
        readonly List<Object> owned = new List<Object>();
        GameObject parent;
        AudioService service;
        AudioMixer mixer;
        readonly AudioCueDefinition[] cues = new AudioCueDefinition[5];
        int subscriptions;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        AudioSource Voice(string name) => parent.transform.Find("AudioRoot/" + name).GetComponent<AudioSource>();
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
            subscriptions = EventBus<SessionReset>.SubscriberCount;
            parent = Own(new GameObject("MusicPlayModeTests"));
            var config = Own(ScriptableObject.CreateInstance<AudioConfigDefinition>());
            var database = Own(ScriptableObject.CreateInstance<ContentDatabase>());
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Game/Audio/Mixer/GameMixer.mixer"); Assert.IsNotNull(mixer);
            using var settings = new SerializedObject(config); settings.FindProperty("mixer").objectReferenceValue = mixer;
            foreach (string bus in new[] { "Sfx", "Ui", "Music", "Ambience" })
                settings.FindProperty(char.ToLowerInvariant(bus[0]) + bus.Substring(1) + "Group").objectReferenceValue = mixer.FindMatchingGroups("Master/" + bus)[0];
            var definitions = new List<ContentDefinition>();
            string[] roles = { "Menu", "Exploration", "Combat", "Editor", "Override" };
            float[] volumes = { .4f, .6f, .8f, .2f, .5f };
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = Own(ScriptableObject.CreateInstance<AudioCueDefinition>()); cues[i] = cue;
                using var edit = new SerializedObject(cue);
                edit.FindProperty("id").stringValue = "cue_music_test_" + i;
                edit.FindProperty("bus").intValue = (int)AudioBus.Music;
                edit.FindProperty("spatialBlend").floatValue = 0; edit.FindProperty("loop").boolValue = true;
                edit.FindProperty("volume").floatValue = volumes[i]; edit.FindProperty("volumeRandom").floatValue = 0;
                edit.FindProperty("pitchRandom").floatValue = 0;
                var clips = edit.FindProperty("clips"); clips.arraySize = 1;
                clips.GetArrayElementAtIndex(0).objectReferenceValue = Own(AudioClip.Create("Silent music " + roles[i], 480000, 1, 48000, false));
                edit.ApplyModifiedPropertiesWithoutUndo(); definitions.Add(cue);
                if (i < 4) settings.FindProperty("music" + roles[i]).objectReferenceValue = cue;
            }
            settings.ApplyModifiedPropertiesWithoutUndo(); database.EditorSetDefinitions(definitions);
            service = new AudioService(config, database, AudioVolumes.Default, parent.transform);
            parent.GetComponentInChildren<AudioRoot>().enabled = false; // This check controls time; normal LateUpdate must not advance it.
            Assert.AreEqual(subscriptions + 1, EventBus<SessionReset>.SubscriberCount);
        }
        [UnityTearDown]
        public IEnumerator Clean()
        {
            service?.Dispose(); service = null;
            Assert.AreEqual(subscriptions, EventBus<SessionReset>.SubscriberCount);
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.Destroy(owned[i]); owned.Clear();
            if (mixer != null) foreach (string bus in new[] { "Master", "Sfx", "Ui", "Music", "Ambience" }) mixer.ClearFloat(bus + "Volume");
            yield return null;
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
        }
        [UnityTest]
        public IEnumerator Crossfade_IsEqualPower()
        {
            var a = Voice("MusicA"); var b = Voice("MusicB");
            service.SetSceneMusicContext("MainMenu"); service.Update(2);
            Assert.AreEqual(MusicState.Menu, service.MusicState); Assert.AreSame(cues[0].Clips[0], a.clip);
            Assert.AreEqual(.4f, a.volume, .001f); Assert.AreEqual(0, b.volume, .001f);
            Assert.IsTrue(a.loop); Assert.IsTrue(a.ignoreListenerPause);
            service.SetSceneMusicContext("World"); service.Update(1);
            float midpoint = Mathf.Cos(Mathf.PI / 4);
            Assert.AreEqual(.4f * midpoint, a.volume, .01f); Assert.AreEqual(.6f * midpoint, b.volume, .01f);
            Assert.AreSame(cues[1].Clips[0], b.clip); Assert.IsTrue(b.loop); Assert.IsTrue(b.ignoreListenerPause);
            Assert.AreEqual(1, a.volume * a.volume / (.4f * .4f) + b.volume * b.volume / (.6f * .6f), .01f);
            service.Update(1); Assert.IsNull(a.clip); Assert.AreEqual(.6f, b.volume, .001f);

            service.ReportCombatSignals(2); service.Update(0); Assert.AreEqual(MusicState.Exploration, service.MusicState);
            service.ReportCombatSignals(1); service.Update(0); Assert.AreEqual(MusicState.Combat, service.MusicState);
            service.Update(.5f);
            float currentA = a.volume, currentB = b.volume;
            service.SetMusicOverride(cues[4].Id, 1); service.Update(0);
            Assert.AreEqual(currentA, a.volume, .001f); Assert.AreEqual(currentB, b.volume, .001f, "An interruption begins at the current gains.");
            Assert.AreEqual(MusicState.Override, service.MusicState);
            service.Update(1); Assert.IsTrue(a.clip == cues[4].Clips[0] || b.clip == cues[4].Clips[0]);
            service.SetMusicOverride(cues[3].Id, 0); service.Update(0);
            Assert.AreEqual(MusicState.Override, service.MusicState);
            Assert.IsTrue(a.clip == cues[3].Clips[0] || b.clip == cues[3].Clips[0], "A new cue replaces an override without a state transition.");
            service.ClearMusicOverride(0); service.Update(0);
            Assert.AreEqual(MusicState.Combat, service.MusicState);

            service.SetMusicOverride(cues[4].Id, 0); service.Update(0);
            EventBus<SessionReset>.Publish(new SessionReset(SessionResetReason.Loaded)); EventBus<SessionReset>.Drain();
            Assert.AreEqual(MusicState.Exploration, service.MusicState, "Reset clears override and combat history in the current World context.");
            service.Update(2); Assert.IsTrue(a.clip == cues[1].Clips[0] || b.clip == cues[1].Clips[0]);
            Assert.AreEqual(MusicState.Exploration, service.Stats.Music);
            service.SetSceneMusicContext("BlueprintLab"); service.Update(2); Assert.AreEqual(MusicState.Editor, service.MusicState);
            service.SetMusicOverride(cues[4].Id, 0); service.Update(0);
            service.SetSceneMusicContext("UnmappedMusicTestScene"); service.Update(2);
            Assert.AreEqual(MusicState.Silent, service.MusicState); Assert.IsNull(a.clip); Assert.IsNull(b.clip);
            Assert.AreEqual(0, a.volume); Assert.AreEqual(0, b.volume);
            Assert.AreEqual(60, parent.GetComponentsInChildren<AudioSource>().Length, "All playback uses the original pools.");
            yield return null;
        }
    }
}
#endif
