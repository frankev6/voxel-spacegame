using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Text;
using Game.App.DevRuntime;
using Game.App.DevRuntime.Scenarios;
using Game.App.Testing;
using Game.Core;
using Game.Core.Diagnostics;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Presentation.Audio;
using Game.Presentation.Vfx;
using Game.Simulation.Weapons;
using Game.Tests.Common;
using Game.Tests.EditMode.Vfx;
using Game.Voxel.Damage;
using Game.Voxel.Model;
using Game.Voxel.PresentationStreams;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Profiling;

namespace Game.Tests.EditMode.DevTools
{
    /// <summary>DEV-011's two functional cases; tiny payloads, direct owner updates, no benchmark loops.</summary>
    public sealed class PerfCommandTests
    {
        Fixture markerFixture;
        bool ownsPlayMode, profilerCaptured, hooksCaptured;
        bool previousProfilerEnabled, previousProfileEditor, previousCpu, previousSkipIntro;
        string previousSaveRoot, previousSettingsPath, playerTestDirectory;
        [SetUp] public void Setup() => PerfMarkers.StopRecording();
        [TearDown] public void Cleanup() => PerfMarkers.StopRecording();
        [UnityTearDown] public IEnumerator LeavePlayerMode()
        {
            Exception disposalFailure = null;
            try { markerFixture?.Dispose(); }
            catch (Exception e) { disposalFailure = e; }
            markerFixture = null;
            try
            {
                if (ownsPlayMode && Application.isPlaying) yield return new ExitPlayMode();
            }
            finally
            {
                ownsPlayMode = false;
                if (profilerCaptured)
                {
                    ProfilerDriver.enabled = previousProfilerEnabled; ProfilerDriver.profileEditor = previousProfileEditor;
                    ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, previousCpu); profilerCaptured = false;
                }
                if (hooksCaptured)
                {
                    GameRootTestHooks.SaveRoot = previousSaveRoot; GameRootTestHooks.SettingsPath = previousSettingsPath;
                    GameRootTestHooks.SkipIntroScreens = previousSkipIntro; hooksCaptured = false;
                }
                if (playerTestDirectory != null && Directory.Exists(playerTestDirectory)) Directory.Delete(playerTestDirectory, true);
                playerTestDirectory = null;
            }
            if (disposalFailure != null) throw disposalFailure;
        }
        sealed class Fixture : IDisposable
        {
            internal readonly EcsTestWorld Test = EcsTestWorld.Create(typeof(FxEventQueueCreateSystem), typeof(FxUploadSystem), typeof(AudioEventDrainSystem));
            internal readonly FxUploadFixture Fx = new();
            internal readonly DevRuntimeHost Host;
            readonly DevRuntimeHost previousHost;
            internal ProjectileStore Projectiles = ProjectileStore.Create(4);
            internal EntityManager Em => Test.EntityManager;
            internal Fixture()
            {
                Services.TryGet(out previousHost); if (previousHost != null) Services.Unregister(previousHost);
                Host = DevRuntimeHost.Create(null); Host.enabled = false;
                Test.World.GetExistingSystemManaged<FxUploadSystem>().Bind(Fx);
                Em.CreateSingleton(Projectiles);
            }
            internal void Flow()
            {
                using var query = Em.CreateEntityQuery(typeof(FxEventQueue)); var queue = query.GetSingleton<FxEventQueue>();
                queue.Access.CompleteAccess(); queue.Queue.Enqueue(new FxEvent { Kind = FxKind.HitKinetic, Pos = float3.zero });
                Test.World.GetExistingSystemManaged<FxUploadSystem>().Update();
            }
            internal string Command(string command)
            {
                var result = Host.Commands.Execute(command, Host.Context);
                Assert.AreEqual(DevResultKind.Ok, result.Kind, result.Text); return result.Text;
            }
            internal double Probe(string source) => ScenarioParser.ParseExpression(source, Host.Probes).Evaluate(Host.Context).Number;
            internal Entity Grid(GridKind kind, bool disabled = false)
            {
                var root = Em.CreateEntity(typeof(VoxelGrid)); Em.SetComponentData(root, new VoxelGrid { Kind = kind });
                if (disabled) Em.AddComponent<Disabled>(root); return root;
            }
            internal void Brick(Entity root, ushort count, BrickDirtyFlags flags, bool enabled = true)
            {
                var entity = Em.CreateEntity(typeof(Brick), typeof(BrickDirty));
                Em.SetComponentData(entity, new Brick { Grid = root, SolidCount = count });
                Em.SetComponentData(entity, new BrickDirty { Flags = flags }); Em.SetComponentEnabled<BrickDirty>(entity, enabled);
            }
            public void Dispose()
            {
                Host.Dispose(); if (previousHost != null) Services.Register(previousHost);
                Test.World.GetExistingSystemManaged<FxUploadSystem>().Bind(null); Fx.Dispose(); Projectiles.Dispose(); Test.Dispose();
            }
        }
        struct WriteStats : IJob
        {
            internal NativeReference<ProjectileStoreStats> Stats;
            public void Execute() => Stats.Value = new ProjectileStoreStats { Live = 3 };
        }
        [Serializable] sealed class Snapshot
        {
            public int entities = 0; public bool worldAvailable = false;
            public Voxels voxel = null; public Combat combat = null; public Marker[] markers = null;
        }
        [Serializable] sealed class Availability { public bool worldAvailable = false; }
        [Serializable] sealed class Voxels
        { public int ships = 0, stations = 0, asteroids = 0, hulkGrids = 0, bricks = 0, dirtyBricks = 0, meshDirty = 0, colliderDirty = 0, chunkBodies = 0, hulks = 0; public long solidCells = 0; }
        [Serializable] sealed class Combat
        { public int projectiles = 0, activeBeams = 0; public FxMetrics fx = null; public AudioMetrics audio = null; }
        [Serializable] sealed class FxMetrics
        { public int drained = 0, culled = 0, budgetDropped = 0, debrisLiveEstimate = 0; public bool bound = false; }
        [Serializable] sealed class AudioMetrics
        { public int drained = 0, started = 0, activeSfxVoices = 0, activeLoopVoices = 0; public bool bound = false; }
        [Serializable] sealed class Marker { public string name = null; public double ms = 0; }

        [Test] public void Json_IsValid()
        {
            using var f = new Fixture();
            var ship = f.Grid(GridKind.Ship); var disabledShip = f.Grid(GridKind.Ship, true);
            f.Grid(GridKind.Station); f.Grid(GridKind.Asteroid); var hulk = f.Grid(GridKind.Hulk); f.Em.AddComponent<Hulk>(hulk);
            f.Brick(ship, 3, BrickDirtyFlags.Mesh); f.Brick(ship, 2, BrickDirtyFlags.Mesh | BrickDirtyFlags.Collider);
            f.Brick(disabledShip, 1, BrickDirtyFlags.Mesh, false);
            var chunk = f.Em.CreateEntity(typeof(ChunkBody)); f.Em.SetComponentData(chunk, new ChunkBody { Live = 1 });
            var inactive = f.Em.CreateEntity(typeof(ChunkBody)); f.Em.SetComponentData(inactive, new ChunkBody { Live = 1 }); f.Em.AddComponent<Disabled>(inactive);
            f.Em.CreateEntity(typeof(ChunkBody));
            var beam = f.Em.CreateEntity(typeof(BeamState)); f.Em.SetComponentData(beam, new BeamState { Active = 1 });
            var dormantBeam = f.Em.CreateEntity(typeof(BeamState)); f.Em.SetComponentData(dormantBeam, new BeamState { Active = 1 }); f.Em.AddComponent<Disabled>(dormantBeam);
            f.Em.CreateEntity(typeof(BeamState));
            f.Projectiles.Release(new WriteStats { Stats = f.Projectiles.Stats }.Schedule(f.Projectiles.Acquire(default)));
            f.Flow();
            var culture = CultureInfo.CurrentCulture; string json;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); json = f.Command("perf voxel=true combat=true markers=true json=true"); }
            finally { CultureInfo.CurrentCulture = culture; }
            StringAssert.DoesNotContain("NaN", json); StringAssert.DoesNotContain("Infinity", json);
            foreach (string name in new[] { "mainMs", "draws", "triangles", "gcAllocBytes", "systemMemoryBytes", "entities" }) StringAssert.Contains("\"" + name + "\":", json);
            var result = JsonUtility.FromJson<Snapshot>(json); Assert.IsNotNull(result); Assert.IsTrue(result.worldAvailable);
            Assert.AreEqual(f.Em.UniversalQuery.CalculateEntityCount(), result.entities);
            Assert.AreEqual(2, result.voxel.ships); Assert.AreEqual(1, result.voxel.stations); Assert.AreEqual(1, result.voxel.asteroids);
            Assert.AreEqual(1, result.voxel.hulkGrids); Assert.AreEqual(1, result.voxel.hulks); Assert.AreEqual(3, result.voxel.bricks);
            Assert.AreEqual(6, result.voxel.solidCells); Assert.AreEqual(2, result.voxel.dirtyBricks); Assert.AreEqual(2, result.voxel.meshDirty);
            Assert.AreEqual(1, result.voxel.colliderDirty); Assert.AreEqual(1, result.voxel.chunkBodies);
            Assert.AreEqual(3, result.combat.projectiles); Assert.AreEqual(1, result.combat.activeBeams);
            Assert.AreEqual(1, result.combat.fx.drained); Assert.IsTrue(result.combat.fx.bound);
            Assert.AreEqual(0, result.combat.audio.activeSfxVoices); Assert.AreEqual(1, f.Probe("fx.drained")); Assert.AreEqual(0, f.Probe("sfx.voices"));
            string plain = f.Command("perf voxel=true combat=true"); StringAssert.Contains("solidCells=6", plain); StringAssert.Contains("projectiles=3", plain);
            string basic = f.Command("perf json=true"); StringAssert.DoesNotContain("\"voxel\"", basic); StringAssert.DoesNotContain("\"combat\"", basic);
            Assert.AreEqual(DevResultKind.Failed, f.Host.Commands.Execute("perf unknown=true", f.Host.Context).Kind);
            Assert.AreEqual(DevResultKind.Failed, f.Host.Commands.Execute("perf json=maybe", f.Host.Context).Kind);
            // Preserve the actual all-sections payload for the spec's python3 -m json.tool acceptance check.
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/playtest/dev011/perf-command.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, json, new UTF8Encoding(false));
            var previous = World.DefaultGameObjectInjectionWorld; World.DefaultGameObjectInjectionWorld = null;
            try
            {
                string absent = f.Command("perf voxel=true combat=true json=true");
                Assert.IsFalse(JsonUtility.FromJson<Availability>(absent).worldAvailable); StringAssert.Contains("\"projectiles\":null", absent);
                StringAssert.Contains("\"fx\":null", absent); Assert.AreEqual(0, f.Probe("fx.drained")); Assert.AreEqual(0, f.Probe("sfx.voices"));
            }
            finally { World.DefaultGameObjectInjectionWorld = previous; }
            Assert.AreEqual(3, JsonUtility.FromJson<Snapshot>(f.Command("perf combat=true json=true")).combat.projectiles, "Cached queries survive a world detachment/rebind.");
        }

        [UnityTest] public IEnumerator Markers_ListsActiveMarkers()
        {
            Assert.IsFalse(Application.isPlaying, "The EditMode case owns its one player-mode transition.");
            previousSaveRoot = GameRootTestHooks.SaveRoot; previousSettingsPath = GameRootTestHooks.SettingsPath;
            previousSkipIntro = GameRootTestHooks.SkipIntroScreens; hooksCaptured = true;
            playerTestDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/TestRuns/dev011-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(playerTestDirectory);
            GameRootTestHooks.SaveRoot = Path.Combine(playerTestDirectory, "saves");
            GameRootTestHooks.SettingsPath = Path.Combine(playerTestDirectory, "settings.json"); GameRootTestHooks.SkipIntroScreens = true;
            previousProfilerEnabled = ProfilerDriver.enabled; previousProfileEditor = ProfilerDriver.profileEditor;
            previousCpu = ProfilerDriver.IsAreaEnabled(ProfilerArea.CPU); profilerCaptured = true;
            ownsPlayMode = true; yield return new EnterPlayMode();
            ProfilerDriver.profileEditor = false; ProfilerDriver.SetAreaEnabled(ProfilerArea.CPU, true); ProfilerDriver.enabled = true;
            markerFixture = new Fixture(); var f = markerFixture;
            UnityEngine.Object.DontDestroyOnLoad(f.Host.gameObject); // Startup scene changes must not destroy the test's counter owner.
            f.Command("perf markers=true");
            using var observed = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "FxUploadSystem.Batch", 1);
            Assert.IsTrue(observed.Valid); Assert.IsTrue(observed.IsRunning);
            long batchWorkNs = 0; bool hasCompletedSample = false;
            // At most four actual player frames, with one real FX upload each. No timing target or benchmark warmup.
            for (int i = 0; i < 4; i++)
            {
                long before = observed.CurrentValue; f.Flow(); batchWorkNs += observed.CurrentValue - before;
                yield return null;
                if (observed.Count > 0 && observed.LastValue > 0) { hasCompletedSample = true; break; }
            }
            Assert.Greater(batchWorkNs, 0, "The canonical FX upload Batch must emit real profiler work.");
            if (!hasCompletedSample)
                Assert.Inconclusive("Profiler frame samples unavailable after four player frames with CPU recording enabled; real FxUploadSystem.Batch work=" +
                    batchWorkNs.ToString(CultureInfo.InvariantCulture) + " ns, Count=" + observed.Count + ". No zero timing accepted (docs/08-tools.md, headless profiler guidance).");
            var result = JsonUtility.FromJson<Snapshot>(f.Command("perf markers=true json=true"));
            Assert.That(result.markers.Length, Is.InRange(1, 15)); bool found = false;
            for (int i = 0; i < result.markers.Length; i++)
            {
                if (result.markers[i].name == "FxUploadSystem.Batch") { found = true; Assert.Greater(result.markers[i].ms, 0); }
                Assert.GreaterOrEqual(result.markers[i].ms, 0);
                if (i > 0) Assert.GreaterOrEqual(result.markers[i - 1].ms, result.markers[i].ms);
            }
            Assert.IsTrue(found, "The real FX upload Batch marker must appear among the last frame's slowest game markers.");
            StringAssert.Contains("FxUploadSystem.Batch", f.Command("perf markers=true")); Assert.AreEqual(1, f.Probe("fx.drained"));
            int recorded = PerfMarkers.Count; f.Host.Perf.Dispose(); f.Host.Perf.Dispose();
            Assert.AreEqual(recorded, PerfMarkers.Count, "Disposing this reader must preserve the shared marker owner's recorders.");
            Assert.Throws<ObjectDisposedException>(() => f.Host.Perf.Format(f.Host.Context, false, false, false, false));
        }
    }
}
