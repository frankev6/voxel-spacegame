#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core.Audio;
using Game.Data;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    public sealed class SfxVoiceAllocatorTests
    {
        readonly List<Object> owned = new List<Object>();
        World world;
        AudioService service;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        [UnitySetUp] public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            service?.Dispose(); service = null; world?.Dispose(); world = null;
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]);
            owned.Clear(); yield return null;
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
        }
        [UnityTest] public IEnumerator MaxSixteenNewPerFrame()
        {
            var config = Own(ScriptableObject.CreateInstance<AudioConfigDefinition>());
            var database = Own(ScriptableObject.CreateInstance<ContentDatabase>());
            var cue = Own(ScriptableObject.CreateInstance<AudioCueDefinition>());
            var parent = Own(new GameObject("Sfx allocator test"));
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Game/Audio/Mixer/GameMixer.mixer");
            using (var edit = new SerializedObject(config))
            {
                edit.FindProperty("mixer").objectReferenceValue = mixer;
                foreach (string bus in new[] { "Sfx", "Ui", "Music", "Ambience" })
                    edit.FindProperty(char.ToLowerInvariant(bus[0]) + bus.Substring(1) + "Group").objectReferenceValue = mixer.FindMatchingGroups("Master/" + bus)[0];
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            using (var edit = new SerializedObject(cue))
            {
                edit.FindProperty("id").stringValue = "cue_allocator_limit";
                edit.FindProperty("bus").intValue = (int)AudioBus.Sfx;
                edit.FindProperty("spatialBlend").floatValue = 1;
                edit.FindProperty("volumeRandom").floatValue = 0;
                edit.FindProperty("pitchRandom").floatValue = 0;
                edit.FindProperty("coalesce").intValue = (int)CoalesceMode.None;
                edit.FindProperty("cooldown").floatValue = 0;
                edit.FindProperty("maxVoices").intValue = 32;
                var clips = edit.FindProperty("clips"); clips.arraySize = 2;
                for (int i = 0; i < 2; i++) clips.GetArrayElementAtIndex(i).objectReferenceValue = Own(AudioClip.Create("SFX limit " + i, 48000, 1, 48000, false));
                edit.ApplyModifiedPropertiesWithoutUndo();
            }
            database.EditorSetDefinitions(new[] { cue });
            service = new AudioService(config, database, AudioVolumes.Default, parent.transform);
            world = new World("SFX allocation"); world.CreateSystem<SfxEventQueueCreateSystem>();
            service.BindTo(world);
            var system = world.GetExistingSystemManaged<AudioEventDrainSystem>();
            using var query = world.EntityManager.CreateEntityQuery(typeof(SfxEventQueue));
            var queue = query.GetSingleton<SfxEventQueue>().Queue;
            for (int i = 0; i < 500; i++) queue.Enqueue(new SfxEvent { Cue = cue.Id.Hash, Volume = 1 });
            system.Update();
            Assert.AreEqual(16, service.Stats.Started); Assert.AreEqual(500, service.Stats.EventsDrained);
            var first = parent.transform.Find("AudioRoot/SfxVoice_00").GetComponent<AudioSource>();
            var second = parent.transform.Find("AudioRoot/SfxVoice_01").GetComponent<AudioSource>();
            Assert.NotNull(first.clip); Assert.AreNotSame(first.clip, second.clip); Assert.AreEqual(1, first.spatialBlend);
            // Warm the complete drain, coalescer and managed allocator before measuring the specified steady state.
            for (int frame = 0; frame < 3; frame++)
            {
                for (int i = 0; i < 500; i++) queue.Enqueue(new SfxEvent { Cue = cue.Id.Hash, Volume = 1 });
                system.Update();
            }
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 500; i++) queue.Enqueue(new SfxEvent { Cue = cue.Id.Hash, Volume = 1 });
            system.Update();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "500 warmed SFX events must not allocate managed memory");
            Assert.IsTrue(queue.IsEmpty()); Assert.LessOrEqual(service.Stats.Started, 16);
            service.Unbind(world); Assert.IsFalse(service.Stats.Bound);
            yield return null;
        }
    }
}
#endif
