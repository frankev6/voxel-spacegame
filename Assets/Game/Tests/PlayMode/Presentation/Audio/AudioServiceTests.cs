#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Game.App.Boot;
using Game.App.Services;
using Game.Core.Audio;
using Game.Data;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    public sealed class AudioServiceTests
    {
        GameObject parent, target;
        AudioConfigDefinition config;
        ContentDatabase database;
        AudioService service;
        AudioMixer mixer;
        [UnitySetUp] public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
            parent = new GameObject("AudioServiceTests"); config = ScriptableObject.CreateInstance<AudioConfigDefinition>();
            database = ScriptableObject.CreateInstance<ContentDatabase>();
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Game/Audio/Mixer/GameMixer.mixer"); Assert.NotNull(mixer);
            using var so = new SerializedObject(config); so.FindProperty("mixer").objectReferenceValue = mixer;
            foreach (string bus in new[] { "Sfx", "Ui", "Music", "Ambience" })
                so.FindProperty(char.ToLowerInvariant(bus[0]) + bus.Substring(1) + "Group").objectReferenceValue = mixer.FindMatchingGroups("Master/" + bus).Single();
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        [UnityTearDown] public IEnumerator Clean()
        {
            service?.Dispose(); service = null;
            Object.Destroy(parent); Object.Destroy(target); Object.Destroy(config); Object.Destroy(database);
            foreach (string bus in new[] { "Master", "Sfx", "Ui", "Music", "Ambience" }) mixer.ClearFloat(bus + "Volume");
            yield return null;
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
        }
        void Construct() => service = new AudioService(config, database, AudioVolumes.Default, parent.transform);
        [UnityTest] public IEnumerator Construct_CreatesOneListenerAnd60Sources()
        {
            Construct(); yield return null;
            Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>().Length);
            var voices = parent.GetComponentsInChildren<AudioSource>(); Assert.AreEqual(60, voices.Length);
            foreach (var voice in voices)
            {
                string bus = voice.name.StartsWith("UiVoice_", StringComparison.Ordinal) ? "Ui" : voice.name == "Ambience" ? "Ambience" : voice.name.StartsWith("Music", StringComparison.Ordinal) || voice.name == "Stinger" ? "Music" : "Sfx";
                Assert.AreSame(mixer.FindMatchingGroups("Master/" + bus).Single(), voice.outputAudioMixerGroup, voice.name);
                Assert.IsFalse(voice.playOnAwake); Assert.AreEqual(bus == "Music" || bus == "Ui", voice.ignoreListenerPause);
            }
        }
        [UnityTest] public IEnumerator ApplyVolumes_SetsMixerOnNextUpdate()
        {
            Construct(); yield return null;
            mixer.GetFloat("MasterVolume", out float before);
            service.ApplyVolumes(new AudioVolumes { Master = .5f, Music = 0, Sfx = 1, Ui = .5f, Ambience = 1 });
            mixer.GetFloat("MasterVolume", out float pending); Assert.AreEqual(before, pending);
            yield return null;
            foreach (var e in new[] { ("MasterVolume", -6.0206f), ("MusicVolume", -80f), ("SfxVolume", 0f), ("UiVolume", -6.0206f), ("AmbienceVolume", 0f) })
            { Assert.IsTrue(mixer.GetFloat(e.Item1, out float db)); Assert.AreEqual(e.Item2, db, .01f); }
        }
        [UnityTest] public IEnumerator Pause_PausesWorldNotUi()
        {
            Construct(); using var pause = new PauseService(); service.BindPause(pause);
            var token = pause.Acquire("Audio test"); yield return null; Assert.IsTrue(AudioListener.pause);
            foreach (var voice in parent.GetComponentsInChildren<AudioSource>())
            {
                bool independent = voice.name.StartsWith("UiVoice_", StringComparison.Ordinal) || voice.name.StartsWith("Music", StringComparison.Ordinal) || voice.name == "Stinger";
                Assert.AreEqual(independent, voice.ignoreListenerPause, voice.name);
            }
            pause.Release(token); yield return null; Assert.IsFalse(AudioListener.pause);
        }
        [UnityTest] public IEnumerator ListenerFollowsTarget_SurvivesTargetDestroy()
        {
            Construct(); target = new GameObject("Listener target"); target.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(10, 20, 30));
            service.SetListenerTarget(target.transform); yield return null;
            var ear = parent.GetComponentInChildren<AudioListener>(); Assert.AreEqual(target.transform.position, ear.transform.position);
            Assert.Less(Quaternion.Angle(target.transform.rotation, ear.transform.rotation), .001f);
            Object.Destroy(target); yield return null; yield return null;
            Assert.IsTrue(ear != null); Assert.AreEqual(new Vector3(2, 3, 4), ear.transform.position);
        }
        [UnityTest] public IEnumerator Dispose_CleansUp()
        {
            Construct(); yield return null;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var info = (NativeArray<SfxCueInfo>)typeof(AudioService).GetField("cueInfo", flags).GetValue(service);
            var map = (NativeHashMap<ulong, int>)typeof(AudioService).GetField("cueSlotByHash", flags).GetValue(service);
            AudioListener.pause = true; service.Dispose(); service.Dispose(); Assert.IsFalse(AudioListener.pause); yield return null;
            Assert.AreEqual(0, parent.GetComponentsInChildren<AudioSource>(true).Length);
            Assert.AreEqual(0, parent.GetComponentsInChildren<AudioListener>(true).Length);
            Assert.Catch<InvalidOperationException>(() => { var value = info[0]; });
            Assert.Catch<InvalidOperationException>(() => { var count = map.Count; });
        }
    }
}
#endif
