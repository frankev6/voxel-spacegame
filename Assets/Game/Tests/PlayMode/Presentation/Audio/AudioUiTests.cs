#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core.Audio;
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
    public sealed class AudioUiTests
    {
        readonly List<Object> owned = new List<Object>();
        GameObject parent;
        AudioService service;
        AudioConfigDefinition config;
        AudioMixer mixer;
        readonly AudioCueDefinition[] cues = new AudioCueDefinition[13];
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        AudioSource Voice(string name) => parent.transform.Find("AudioRoot/" + name).GetComponent<AudioSource>();
        [UnitySetUp] public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
            parent = Own(new GameObject("AudioUiTests")); config = Own(ScriptableObject.CreateInstance<AudioConfigDefinition>());
            var database = Own(ScriptableObject.CreateInstance<ContentDatabase>());
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Game/Audio/Mixer/GameMixer.mixer");
            using var settings = new SerializedObject(config); settings.FindProperty("mixer").objectReferenceValue = mixer;
            foreach (string bus in new[] { "Sfx", "Ui", "Music", "Ambience" })
                settings.FindProperty(char.ToLowerInvariant(bus[0]) + bus.Substring(1) + "Group").objectReferenceValue = mixer.FindMatchingGroups("Master/" + bus)[0];
            var definitions = new List<ContentDefinition>();
            for (int i = 0; i < cues.Length; i++)
            {
                var cue = Own(ScriptableObject.CreateInstance<AudioCueDefinition>()); cues[i] = cue;
                using var edit = new SerializedObject(cue);
                edit.FindProperty("id").stringValue = "cue_test_ui_" + i;
                edit.FindProperty("bus").intValue = i == 11 ? (int)AudioBus.Music : i == 12 ? (int)AudioBus.Sfx : (int)AudioBus.Ui;
                edit.FindProperty("spatialBlend").floatValue = 0; edit.FindProperty("cooldown").floatValue = i == 0 ? .05f : 0;
                edit.FindProperty("volumeRandom").floatValue = 0; edit.FindProperty("pitchRandom").floatValue = 0;
                var clips = edit.FindProperty("clips"); clips.arraySize = 2;
                for (int c = 0; c < 2; c++) clips.GetArrayElementAtIndex(c).objectReferenceValue = Own(AudioClip.Create("UI test " + i + "-" + c, 24000, 1, 48000, false));
                edit.ApplyModifiedPropertiesWithoutUndo(); definitions.Add(cue);
                if (i < 11) settings.FindProperty("uiSounds").GetArrayElementAtIndex(i).objectReferenceValue = cue;
            }
            settings.ApplyModifiedPropertiesWithoutUndo(); database.EditorSetDefinitions(definitions);
            service = new AudioService(config, database, AudioVolumes.Default, parent.transform);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Clean()
        {
            service?.Dispose(); service = null;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.Destroy(owned[i]); owned.Clear();
            foreach (string bus in new[] { "Master", "Sfx", "Ui", "Music", "Ambience" }) mixer.ClearFloat(bus + "Volume");
            yield return null;
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
        }
        [UnityTest] public IEnumerator Hover_RespectsCooldown()
        {
            service.PlayUi(UiSound.Hover); var first = Voice("UiVoice_0").clip; Assert.NotNull(first);
            service.PlayUi(UiSound.Hover); service.PlayCue(cues[0].Id);
            Assert.IsNull(Voice("UiVoice_1").clip, "UI id and enum paths share per-cue cooldown");
            yield return new WaitForSecondsRealtime(.06f);
            service.PlayUi(UiSound.Hover); Assert.NotNull(Voice("UiVoice_1").clip);
            Assert.AreNotSame(first, Voice("UiVoice_1").clip, "A multi-clip cue cannot repeat its last clip");
        }
        [UnityTest] public IEnumerator NinthVoice_StealsOldest()
        {
            for (int i = 0; i < 9; i++) service.PlayUi((UiSound)i);
            Assert.Contains(Voice("UiVoice_0").clip, (System.Collections.ICollection)cues[8].Clips);
            for (int i = 1; i < 8; i++) Assert.Contains(Voice("UiVoice_" + i).clip, (System.Collections.ICollection)cues[i].Clips);
            Assert.AreEqual(60, parent.GetComponentsInChildren<AudioSource>().Length);
            service.PlayCue(cues[12].Id); var sfx = Voice("SfxVoice_00"); Assert.NotNull(sfx.clip); Assert.AreEqual(0, sfx.spatialBlend);
            service.PlayCueAt(cues[12].Id, new Unity.Mathematics.float3(100), .4f);
            Assert.AreEqual(.4f, Voice("SfxVoice_01").volume, .001f); Assert.AreEqual(0, Voice("SfxVoice_01").spatialBlend);
            yield return null;
        }
        [UnityTest] public IEnumerator Stinger_DucksAndRestoresMusic()
        {
            var a = Voice("MusicA"); var b = Voice("MusicB"); a.volume = .7f; b.volume = .4f;
            service.PlayStinger(cues[11].Id); var stinger = Voice("Stinger"); Assert.IsTrue(stinger.isPlaying);
            service.Update(.2f); float target = Mathf.Pow(10,-8f/20);
            Assert.AreEqual(.7f * target, a.volume, .001f); Assert.AreEqual(.4f * target, b.volume, .001f);
            for (int i = 0; i < 10; i++) service.Update(.01f);
            Assert.AreEqual(.7f * target, a.volume, .001f, "Ducking must not compound");
            double deadline = Time.realtimeSinceStartupAsDouble + 2;
            while (stinger.isPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(stinger.isPlaying, "One-shot stinger must end");
            service.Update(1); Assert.AreEqual(.7f, a.volume, .001f); Assert.AreEqual(.4f,b.volume,.001f);
        }
        [UnityTest, Category("Performance")] public IEnumerator PlayUi_NoGcAlloc()
        {
            for (int i = 0; i < 32; i++) service.PlayUi(UiSound.Click);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) service.PlayUi(UiSound.Click);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, bytes, "Managed allocation over 1000 warmed PlayUi calls"); yield return null;
        }
    }
}
#endif
