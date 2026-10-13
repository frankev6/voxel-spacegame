#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Data;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    /// <summary>AUD-009's exact three scenarios: production bridge and pooled service, synthetic runtime test clips only.</summary>
    public sealed class AudioLoopTests
    {
        readonly List<Object> owned = new List<Object>();
        GameObject parent;
        AudioConfigDefinition config;
        AudioMixer mixer;
        AudioService service;
        EcsTestWorld world;
        AudioLoopBridgeSystem bridge;
        Entity gameplay;
        NativeLeakDetectionMode previousLeakMode;
        EntityManager Em => world.EntityManager;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        AudioSource Voice(int index) => parent.transform.Find("AudioRoot/LoopVoice_" + index).GetComponent<AudioSource>();

        [UnitySetUp] public IEnumerator SetUp()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
            previousLeakMode = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            LogAssert.ignoreFailingMessages = false;
            parent = Own(new GameObject("AudioLoopTests")); config = Own(ScriptableObject.CreateInstance<AudioConfigDefinition>());
            var database = Own(ScriptableObject.CreateInstance<ContentDatabase>());
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Game/Audio/Mixer/GameMixer.mixer"); Assert.NotNull(mixer);
            var engine = Cue("cue_test_loop_engine", .6f, 1); var beam = Cue("cue_test_loop_beam", .8f, 1.4f);
            database.EditorSetDefinitions(new ContentDefinition[] { engine, beam });
            using (var settings = new SerializedObject(config))
            {
                settings.FindProperty("mixer").objectReferenceValue = mixer;
                foreach (string bus in new[] { "Sfx", "Ui", "Music", "Ambience" })
                    settings.FindProperty(char.ToLowerInvariant(bus[0]) + bus.Substring(1) + "Group").objectReferenceValue = mixer.FindMatchingGroups("Master/" + bus)[0];
                settings.FindProperty("engineLoop").objectReferenceValue = engine; settings.FindProperty("beamLoop").objectReferenceValue = beam;
                settings.FindProperty("loopFadeSeconds").floatValue = .1f; settings.FindProperty("beamLoopRadius").floatValue = 300;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            world = EcsTestWorld.Create(typeof(AudioLoopBridgeSystem), typeof(AudioEventDrainSystem), typeof(SfxEventQueueCreateSystem));
            gameplay = world.AddGameplayActive(); bridge = world.World.GetExistingSystemManaged<AudioLoopBridgeSystem>();
            service = new AudioService(config, database, AudioVolumes.Default, parent.transform);
            parent.GetComponentInChildren<AudioRoot>().enabled = false; // Drive exact presentation dt in the test.
            service.BindTo(world.World); service.Update(0);
        }
        AudioCueDefinition Cue(string id, float volume, float pitch)
        {
            var cue = Own(ScriptableObject.CreateInstance<AudioCueDefinition>());
            using var edit = new SerializedObject(cue);
            edit.FindProperty("id").stringValue = id; edit.FindProperty("volume").floatValue = volume;
            edit.FindProperty("pitch").floatValue = pitch; edit.FindProperty("loop").boolValue = true;
            edit.FindProperty("volumeRandom").floatValue = 0; edit.FindProperty("pitchRandom").floatValue = 0;
            var clips = edit.FindProperty("clips"); clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = Own(AudioClip.Create(id, 48000, 1, 48000, false));
            edit.ApplyModifiedPropertiesWithoutUndo(); return cue;
        }
        [UnityTearDown] public IEnumerator Clean()
        {
            try { service?.Dispose(); service = null; world?.Dispose(); world = null; }
            finally { NativeLeakDetection.Mode = previousLeakMode; }
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.Destroy(owned[i]); owned.Clear();
            if (mixer != null) foreach (string bus in new[] { "Master", "Sfx", "Ui", "Music", "Ambience" }) mixer.ClearFloat(bus + "Volume");
            yield return null; LogAssert.NoUnexpectedReceived();
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
        }
        Entity Root(float3 position = default, bool player = false, quaternion rotation = default)
        {
            if (math.all(rotation.value == 0)) rotation = quaternion.identity;
            var root = Em.CreateEntity(typeof(LocalToWorld));
            Em.SetComponentData(root, new LocalToWorld { Value = float4x4.TRS(position, rotation, new float3(1)) });
            if (player) Em.AddComponent<PlayerShip>(root); return root;
        }
        Entity Thruster(Entity root, float intensity)
        {
            var entity = Em.CreateEntity(typeof(FunctionalBlock), typeof(ThrusterState));
            Em.SetComponentData(entity, new FunctionalBlock { Grid = root }); Em.SetComponentData(entity, new ThrusterState { Intensity = intensity }); return entity;
        }
        Entity Beam(Entity root, float3 muzzle)
        {
            var entity = Em.CreateEntity(typeof(FunctionalBlock), typeof(BeamState));
            Em.SetComponentData(entity, new FunctionalBlock { Grid = root }); Em.SetComponentData(entity, new BeamState { MuzzleLocal = muzzle, Active = 1 }); return entity;
        }
        void Step(float dt) { bridge.Update(); service.Update(dt); }
        void Muzzle(Entity beam, float distance)
        { var state = Em.GetComponentData<BeamState>(beam); state.MuzzleLocal = new float3(0,0,distance); Em.SetComponentData(beam, state); }

        [Test] public void EngineVolumeFollowsIntensity()
        {
            var root = Root(new float3(12,3,5), true); var low = Thruster(root, .2f); var high = Thruster(root, .75f);
            Thruster(Root(new float3(99), false), 1); // A stronger NPC thruster never controls the player loop.
            Step(.05f); var source = Voice(0);
            Assert.AreEqual(.3f, source.volume, .001f, "Half the 0.1s envelope, multiplied by the cue's 0.6 gain once.");
            Assert.AreEqual(1.1f, source.pitch, .001f); Assert.AreEqual(new Vector3(12,3,5), source.transform.position);
            Assert.IsTrue(source.loop); Assert.IsFalse(source.ignoreListenerPause); Assert.AreEqual(1, service.Stats.ActiveLoopVoices);
            var clip = source.clip; Step(.05f); Assert.AreEqual(.45f, source.volume, .001f); Assert.AreSame(clip, source.clip);
            Em.SetComponentData(high, new ThrusterState { Intensity = .1f }); Em.SetComponentData(low, new ThrusterState { Intensity = .25f });
            Step(.025f); Assert.AreEqual(.3f, source.volume, .001f); Assert.AreEqual(.9f, source.pitch, .001f);
            Step(.025f); Assert.AreEqual(.15f, source.volume, .001f);
            Em.SetComponentData(high, new ThrusterState { Intensity = float.NaN }); Step(.1f); Assert.AreEqual(.15f, source.volume, .001f);
            Em.SetComponentData(low, new ThrusterState()); Step(.1f); Assert.AreEqual(0, source.volume); Assert.IsNull(source.clip); Assert.AreEqual(0, service.Stats.ActiveLoopVoices);
            Em.SetComponentData(low, new ThrusterState { Intensity = 1 }); Step(.1f); Assert.NotNull(source.clip);
            Em.RemoveComponent<GameplayActive>(gameplay); bridge.Update(); Assert.AreEqual(0, source.volume); Assert.IsNull(source.clip, "Scene stop is immediate, independent of fade duration.");
            Assert.AreEqual(60, parent.GetComponentsInChildren<AudioSource>().Length); Assert.IsNull(Voice(7).clip);
        }

        [Test] public void NearestSixBeams_StableAssignment()
        {
            var root = Root(rotation: quaternion.RotateY(math.PI * .5f)); var beams = new Entity[8]; var assigned = new int[6];
            for (int i = 0; i < beams.Length; i++) beams[i] = Beam(root, new float3(0,0,(i + 1) * 10));
            Step(.1f); Assert.AreEqual(6, service.Stats.ActiveLoopVoices);
            for (int i = 0; i < beams.Length; i++)
            {
                Assert.AreEqual(i < 6, bridge.TryGetBeamVoice(beams[i], out int voice));
                if (i >= 6) continue;
                assigned[i] = voice; Assert.AreEqual(i + 1, voice);
                Assert.AreEqual(.8f, Voice(voice).volume, .001f); Assert.AreEqual(1.4f, Voice(voice).pitch, .001f);
                var expected = math.transform(Em.GetComponentData<LocalToWorld>(root).Value, Em.GetComponentData<BeamState>(beams[i]).MuzzleLocal);
                Assert.Less(math.distance(expected, Voice(voice).transform.position), .0001f, "Beam start uses the owning root's world matrix.");
            }
            Muzzle(beams[5], 5); Muzzle(beams[0], 55); Step(.1f);
            for (int i = 0; i < 6; i++) { Assert.IsTrue(bridge.TryGetBeamVoice(beams[i], out int voice)); Assert.AreEqual(assigned[i], voice, "Rank changes retain ownership."); }
            var old = Em.GetComponentData<BeamState>(beams[0]); old.Active = 0; Em.SetComponentData(beams[0], old);
            Muzzle(beams[6], 7); Muzzle(beams[7], 50); Step(.1f);
            Assert.IsFalse(bridge.TryGetBeamVoice(beams[0], out _)); Assert.IsTrue(bridge.TryGetBeamVoice(beams[6], out int newcomer)); Assert.AreEqual(assigned[0], newcomer);
            for (int i = 1; i < 6; i++) { Assert.IsTrue(bridge.TryGetBeamVoice(beams[i], out int voice)); Assert.AreEqual(assigned[i], voice); }
            Assert.IsFalse(bridge.TryGetBeamVoice(beams[7], out _), "An exact distance tie keeps the lower entity index in the six-element set.");
            Muzzle(beams[6], 301); Step(.1f); Assert.IsFalse(bridge.TryGetBeamVoice(beams[6], out _), "Radius uses the transformed muzzle, not the root origin.");
            Em.DestroyEntity(root); Step(.1f); Assert.AreEqual(0, service.Stats.ActiveLoopVoices, "Active stale beam entities cannot retain audio after their root disappears.");
            for (int i = 1; i <= 6; i++) Assert.IsNull(Voice(i).clip);
            Assert.IsNull(Voice(7).clip); Assert.AreEqual(60, parent.GetComponentsInChildren<AudioSource>().Length);
        }

        [Test] public void NullCue_Silent()
        {
            var root = Root(player: true); Thruster(root, 1); for (int i = 0; i < 8; i++) Beam(root, new float3(i * 10,0,0));
            Step(.1f); Assert.AreEqual(7, service.Stats.ActiveLoopVoices);
            using (var edit = new SerializedObject(config))
            { edit.FindProperty("engineLoop").objectReferenceValue = null; edit.FindProperty("beamLoop").objectReferenceValue = null; edit.ApplyModifiedPropertiesWithoutUndo(); }
            Step(.001f); Assert.AreEqual(0, service.Stats.ActiveLoopVoices);
            for (int i = 0; i < 8; i++) { Assert.IsNull(Voice(i).clip); Assert.AreEqual(0, Voice(i).volume); Assert.IsFalse(Voice(i).isPlaying); }
            Step(.1f); Assert.AreEqual(0, service.Stats.ActiveLoopVoices); Assert.AreEqual(60, parent.GetComponentsInChildren<AudioSource>().Length);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
