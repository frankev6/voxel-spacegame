#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Game.App.Boot;
using Game.Core;
using Game.Core.Audio;
using Game.Core.Components;
using Game.Data;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using Game.Simulation.Commands;
using Game.Simulation.Flight;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Tests.Common.Perf;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Presentation.Audio
{
    /// <summary>Shared real-audio fixture. All generated objects/clips are disposable; authored content is never edited.</summary>
    internal sealed class AudioMeasurementFixture : IDisposable
    {
        public const int Warmup = 60, Samples = 300;
        public const ulong Seed = 64001;
        public const float Dt = 1f / 60;
        readonly List<Object> owned = new List<Object>();
        readonly ContentHash[] combat = new ContentHash[10];
        readonly Entity[] thrusters = new Entity[10], beams = new Entity[8];
        NativeArray<SfxEvent> events;
        readonly SfxEventQueue queue;
        readonly Entity root;
        readonly AudioConfigDefinition config;
        Unity.Mathematics.Random random = RngStreams.For(Seed, RngPurpose.Test, 64);
        public EcsTestWorld World { get; private set; }
        public AudioService Service { get; private set; }
        public AudioEventDrainSystem Drain { get; private set; }
        public AudioLoopBridgeSystem Loops { get; private set; }
        public GameObject Parent { get; private set; }
        int started;
        T Own<T>(T value) where T : Object { owned.Add(value); return value; }

        public AudioMeasurementFixture()
        {
            try
            {
                var source = Resources.Load<GameRoot>("GameRoot").Config.ContentDatabase;
                AudioConfigDefinition authored = null;
                foreach (var definition in source.Definitions)
                    if (definition is AudioConfigDefinition audio && audio.IdString == "config_audio") authored = audio;
                Assert.NotNull(authored, "AUD-007's validated config is a real prerequisite.");
                config = Own(Object.Instantiate(authored));
                var database = Own(ScriptableObject.CreateInstance<ContentDatabase>());
                var definitions = new List<ContentDefinition>(source.Definitions);
                var engine = LoopCue("cue_audio_perf_engine"); var beam = LoopCue("cue_audio_perf_beam");
                definitions.Add(engine); definitions.Add(beam); database.EditorSetDefinitions(definitions);
                using (var edit = new SerializedObject(config))
                {
                    edit.FindProperty("engineLoop").objectReferenceValue = engine;
                    edit.FindProperty("beamLoop").objectReferenceValue = beam;
                    edit.ApplyModifiedPropertiesWithoutUndo();
                }
                var cues = new[] { config.ImpactKinetic, config.ImpactEnergy, config.ImpactExplosive, config.ImpactRam,
                    config.ExplosionSmall, config.ExplosionLarge, config.ShieldHit, config.CellBreak, config.BreakOff, config.ShipDeath };
                for (int i = 0; i < cues.Length; i++)
                { Assert.NotNull(cues[i]); Assert.Greater(cues[i].Clips.Count, 0); combat[i] = cues[i].Id.Hash; }
                Parent = Own(new GameObject("AUD-013 measurement"));
                Service = new AudioService(config, database, AudioVolumes.Default, Parent.transform);
                Parent.GetComponentInChildren<AudioRoot>().enabled = false; // Only the measured calls advance the service.
                World = EcsTestWorld.Create(typeof(SfxEventQueueCreateSystem), typeof(AudioEventDrainSystem), typeof(AudioLoopBridgeSystem));
                World.AddGameplayActive(); Service.BindTo(World.World); Service.SetSceneMusicContext("World");
                Drain = World.World.GetExistingSystemManaged<AudioEventDrainSystem>(); Loops = World.World.GetExistingSystemManaged<AudioLoopBridgeSystem>();
                queue = World.GetSingleton<SfxEventQueue>(); events = new NativeArray<SfxEvent>(4096, Allocator.Persistent);
                var em = World.EntityManager; root = em.CreateEntity(typeof(PlayerShip), typeof(LocalToWorld));
                em.SetComponentData(root, new LocalToWorld { Value = float4x4.identity });
                for (int i = 0; i < thrusters.Length; i++)
                {
                    thrusters[i] = em.CreateEntity(typeof(FunctionalBlock), typeof(ThrusterState));
                    em.SetComponentData(thrusters[i], new FunctionalBlock { Grid = root, InstanceIndex = i });
                }
                for (int i = 0; i < beams.Length; i++)
                {
                    beams[i] = em.CreateEntity(typeof(FunctionalBlock), typeof(BeamState));
                    em.SetComponentData(beams[i], new FunctionalBlock { Grid = root, InstanceIndex = i });
                }
                Prepare(500, 0); AudioFrame(500); CheckFrame(500, true); // Initialize playback, system markers and all pooled loop voices.
            }
            catch { Dispose(); throw; }
        }
        AudioCueDefinition LoopCue(string id)
        {
            var cue = Own(ScriptableObject.CreateInstance<AudioCueDefinition>());
            using var edit = new SerializedObject(cue); edit.FindProperty("id").stringValue = id;
            edit.FindProperty("loop").boolValue = true; edit.FindProperty("volumeRandom").floatValue = 0; edit.FindProperty("pitchRandom").floatValue = 0;
            var clips = edit.FindProperty("clips"); clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = Own(AudioClip.Create(id, 480000, 1, 48000, false));
            edit.ApplyModifiedPropertiesWithoutUndo(); return cue;
        }
        public void Prepare(int count, int frame)
        {
            Assert.That(count, Is.InRange(0, events.Length));
            float3 listener = Service.ListenerPosition;
            for (int i = 0; i < count; i++)
                events[i] = new SfxEvent { Cue = combat[random.NextInt(combat.Length)], Pos = listener + random.NextFloat3(new float3(-200), new float3(200)),
                    Volume = random.NextFloat(.2f, 1), Flags = i % 4 == 0 ? SfxEventFlags.PlayerSource : SfxEventFlags.None };
            var em = World.EntityManager;
            em.SetComponentData(root, new LocalToWorld { Value = float4x4.Translate(new float3(frame % 21 - 10,0,0)) });
            for (int i = 0; i < thrusters.Length; i++) em.SetComponentData(thrusters[i], new ThrusterState { Intensity = i == 0 ? (frame % 120 < 60 ? .75f : .25f) : .1f });
            for (int i = 0; i < beams.Length; i++) em.SetComponentData(beams[i], new BeamState { Active = 1, MuzzleLocal = new float3(10 + i * 20,0,0) });
        }
        public void Enqueue(int count)
        {
            queue.Access.CompleteAccess(); // ADR-0067 managed-producer boundary, including the allocation measurement.
            for (int i = 0; i < count; i++) queue.Queue.Enqueue(events[i]);
        }
        public void DrainOnly() { Drain.Update(); started += Service.Stats.Started; }
        public void ServiceAndLoops() { Loops.Update(); Service.Update(Dt); }
        public void AudioFrame(int count)
        { Enqueue(count); DrainOnly(); Loops.Update(); Service.PlayUi(UiSound.Click); Service.Update(Dt); }
        public void CheckFrame(int count, bool loops)
        {
            var stats = Service.Stats;
            Assert.IsTrue(queue.Queue.IsEmpty()); Assert.IsTrue(stats.Bound); Assert.AreEqual(count, stats.EventsDrained);
            Assert.AreEqual(0, stats.EventsOverHardCap); Assert.AreEqual(0, stats.Unresolved); Assert.AreEqual(0, stats.Culled);
            Assert.LessOrEqual(stats.Started, 16); Assert.LessOrEqual(stats.ActiveSfxVoices, 40);
            if (loops) Assert.AreEqual(7, stats.ActiveLoopVoices, "Player engine plus six real bridge-selected beam loops.");
        }
        public void CheckCompleted()
        { Assert.Greater(started, 0, "A silent/unbound drain cannot satisfy the playback benchmark."); Assert.AreEqual(60, Parent.GetComponentsInChildren<AudioSource>().Length); }
        public void Dispose()
        {
            Service?.Dispose(); Service = null; World?.Dispose(); World = null;
            if (events.IsCreated) events.Dispose();
            if (config != null && config.Mixer != null)
                foreach (string bus in new[] { "Master", "Sfx", "Ui", "Music", "Ambience" }) config.Mixer.ClearFloat(bus + "Volume");
            for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.Destroy(owned[i]); owned.Clear();
        }
    }

    internal sealed class AudioReferenceScope : IDisposable
    {
        readonly bool burst = BurstCompiler.Options.EnableBurstCompilation, safety = BurstCompiler.Options.EnableBurstSafetyChecks,
            synchronous = BurstCompiler.Options.EnableBurstCompileSynchronously, debugger = JobsUtility.JobDebuggerEnabled;
        readonly NativeLeakDetectionMode leaks = NativeLeakDetection.Mode;
        public AudioReferenceScope()
        {
            try
            {
                BurstCompiler.Options.EnableBurstCompilation = true; BurstCompiler.Options.EnableBurstSafetyChecks = false;
                BurstCompiler.Options.EnableBurstCompileSynchronously = true; JobsUtility.JobDebuggerEnabled = false; NativeLeakDetection.Mode = NativeLeakDetectionMode.Disabled;
                PerfSettings.AssertReference();
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            BurstCompiler.Options.EnableBurstCompilation = burst; BurstCompiler.Options.EnableBurstSafetyChecks = safety;
            BurstCompiler.Options.EnableBurstCompileSynchronously = synchronous; JobsUtility.JobDebuggerEnabled = debugger; NativeLeakDetection.Mode = leaks;
        }
    }

    internal sealed class AudioCostRecorder : IDisposable
    {
        readonly ProfilerRecorder[] recorders;
        public AudioCostRecorder(string gate, double budget, params string[] names)
        {
            recorders = new ProfilerRecorder[names.Length];
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            try
            {
                for (int i = 0; i < names.Length; i++)
                {
                    foreach (var handle in handles)
                    {
                        if (ProfilerRecorderHandle.GetDescription(handle).Name != names[i]) continue;
                        recorders[i] = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.StartImmediately); break;
                    }
                    if (recorders[i].Valid && recorders[i].IsRunning) continue;
                    // Retain a false/null measurement rather than manufacturing a passing zero or switching timers.
                    new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples(gate, new[] { double.NaN }).Write(gate, budget);
                    Assert.Inconclusive("Audio timing unavailable: missing active profiler marker " + names[i]);
                }
            }
            catch { Dispose(); throw; }
        }
        public long CurrentNanoseconds
        { get { long total = 0; for (int i = 0; i < recorders.Length; i++) total += recorders[i].CurrentValue; return total; } }
        public long CurrentNanosecondsAt(int index) => recorders[index].CurrentValue;
        public void Dispose() { for (int i = 0; i < recorders.Length; i++) if (recorders[i].Valid) recorders[i].Dispose(); }
    }

    /// <summary>AUD-013's three timing measurements; exact budgets remain advisory through PerfRun/ADR-0042.</summary>
    public sealed class AudioPerfTests
    {
        [UnitySetUp] public IEnumerator SetUp()
        { if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject); yield return null; }
        [UnityTearDown] public IEnumerator Clean()
        { yield return null; LogAssert.NoUnexpectedReceived(); if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; } }

        IEnumerator MeasureDrain(int count, double budget, string gate)
        {
            using var reference = new AudioReferenceScope(); using var fixture = new AudioMeasurementFixture();
            string marker = EntityManager.EntityManagerDebug.GetSystemProfilerMarkerName(fixture.World.World, fixture.Drain.SystemHandle);
            using var recorder = new AudioCostRecorder(gate, budget, marker);
            int frame = 0; double elapsed = 0;
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName, AudioMeasurementFixture.Warmup, AudioMeasurementFixture.Samples).Custom(gate, () => elapsed);
            yield return run.RunFrames(() =>
            {
                fixture.Prepare(count, frame++); fixture.Enqueue(count);
                long before = recorder.CurrentNanoseconds; fixture.DrainOnly(); elapsed = (recorder.CurrentNanoseconds - before) * 1e-6;
                fixture.CheckFrame(count, false);
            });
            fixture.CheckCompleted(); Finish(run, gate, budget);
        }
        internal static void Finish(PerfRun run, string gate, double budget, params string[] components)
        {
            var stat = run.Get(gate); Assert.AreEqual(AudioMeasurementFixture.Samples, stat.Samples);
            bool available = stat.Median > 0 && !double.IsInfinity(stat.Median);
            foreach (string component in components)
            {
                var part = run.Get(component); Assert.AreEqual(AudioMeasurementFixture.Samples, part.Samples);
                available &= part.Median > 0 && !double.IsInfinity(part.Median);
            }
            if (!available)
            {
                string unavailable = gate + ".Unavailable";
                run.AddSamples(unavailable, new[] { double.NaN }).Write(unavailable, budget);
                Assert.Inconclusive("Audio timing unavailable: no finite positive median; raw samples and false/null JSON retained.");
            }
            run.Write(gate, budget);
            run.AssertMedian(gate, budget);
            TestContext.WriteLine($"AUD-013 {gate}: seed=64001, warmup=60, samples={stat.Samples}, median={stat.Median:G8}ms, p95={stat.P95:G8}ms, target<={budget}ms; zero tuning passes.");
        }
        [UnityTest, Performance, Category("Performance")] public IEnumerator Drain500_Smoke() => MeasureDrain(500, .15, "AudioDrain500");
        [UnityTest, Performance, Category("Performance")] public IEnumerator Drain4096() => MeasureDrain(4096, .4, "AudioDrain4096");
        [UnityTest, Performance, Category("Performance")] public IEnumerator ServiceAndLoops_Smoke()
        {
            using var reference = new AudioReferenceScope(); using var fixture = new AudioMeasurementFixture();
            string loop = EntityManager.EntityManagerDebug.GetSystemProfilerMarkerName(fixture.World.World, fixture.Loops.SystemHandle);
            const string gate = "AudioServiceAndLoops"; using var recorder = new AudioCostRecorder(gate, .05, loop, "AudioService.Update");
            int frame = 0; double elapsed = 0, loopElapsed = 0, serviceElapsed = 0;
            const string bridgeCost = "AudioLoopBridge", serviceCost = "AudioServiceUpdate";
            var run = new PerfRun(TestContext.CurrentContext.Test.FullName, AudioMeasurementFixture.Warmup, AudioMeasurementFixture.Samples)
                .Custom(gate, () => elapsed).Custom(bridgeCost, () => loopElapsed).Custom(serviceCost, () => serviceElapsed);
            yield return run.RunFrames(() =>
            {
                fixture.Prepare(0, frame++);
                long beforeLoop = recorder.CurrentNanosecondsAt(0), beforeService = recorder.CurrentNanosecondsAt(1);
                fixture.ServiceAndLoops();
                loopElapsed = (recorder.CurrentNanosecondsAt(0) - beforeLoop) * 1e-6;
                serviceElapsed = (recorder.CurrentNanosecondsAt(1) - beforeService) * 1e-6;
                elapsed = loopElapsed + serviceElapsed;
                Assert.AreEqual(7, fixture.Service.Stats.ActiveLoopVoices);
            });
            fixture.CheckCompleted(); Finish(run, gate, .05, bridgeCost, serviceCost);
        }
    }
}
#endif
