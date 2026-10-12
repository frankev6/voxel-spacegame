using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Game.App.Boot;
using Game.App.Settings;
using Game.App.Testing;
using Game.Core;
using Game.Core.Settings;
using Game.Data.Components;
using Game.Persistence.Services;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.Boot
{
    /// <summary>FND-005: construction order and reverse disposal, failure cleanup, boot singletons and redirected paths (SPEC-01 §5.1–§5.3).
    /// Each test that tears the boot down boots again in TearDown, so later tests see a normal game.</summary>
    public sealed class CompositionTests
    {
        static readonly string[] ExpectedSteps = { "1 settings", "2 content", "3 input", "4 audio", "5 pause", "6 save", "7 flow", "8 session", "9 ui", "11 camera", "14 settings applier", "15 snapshot", "16 dev runtime" };

        string tempRoot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameComposition.FailAfterStep = null;
            GameComposition.StopPlayModeOnFailure = true;
            if (GameRootTestHooks.SaveRoot != null || GameRootTestHooks.SettingsPath != null)
            {
                GameRootTestHooks.Reset();
                if (GameRoot.Instance != null) { Object.Destroy(GameRoot.Instance.gameObject); yield return null; }
            }
            if (GameRoot.Instance == null) { Bootstrap.Boot(); yield return null; }
            if (tempRoot != null && Directory.Exists(tempRoot)) { try { Directory.Delete(tempRoot, true); } catch (IOException) { } }
            tempRoot = null;
        }

        static IEnumerator Shutdown()
        {
            if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
            yield return null;
            Assert.IsNull(GameRoot.Instance);
        }

        [UnityTest]
        public IEnumerator Order_AndReverseDispose()
        {
            yield return null;
            var composition = GameRoot.Instance.Composition;
            CollectionAssert.AreEqual(ExpectedSteps, composition.StepLog, "SPEC-01 §5.2 order");
            var built = composition.DisposeStack.Select(d => d.GetType().Name).ToList();
            CollectionAssert.AreEqual(new[] { "SettingsService", "ContentService", "RenderConfigLifetime", "BlueprintLibrary", "RegistryLifetime", "InputService", "AudioService", "PauseService", "SaveService", "SceneFlow", "GameSession", "FiringRangeService", "InputServiceBridge", "UIRoot", "CameraRigService", "SettingsApplier", "DevRuntimeHost" }, built);
            yield return Shutdown();
            built.Reverse();
            CollectionAssert.AreEqual(built, GameComposition.LastDisposeLog, "disposed in reverse");
            Assert.AreEqual(0, Game.Core.Services.Count, "services reset");
        }

        [UnityTest]
        public IEnumerator ThrowingStep_DisposesEarlier()
        {
            yield return Shutdown();
            DefaultWorldInitialization.Initialize(Bootstrap.WorldName, false);
            var world = World.DefaultGameObjectInjectionWorld;
            tempRoot = Path.Combine(Application.temporaryCachePath, "CompositionTests_" + Guid.NewGuid().ToString("N"));
            GameComposition.FailAfterStep = "5 pause";
            GameComposition.StopPlayModeOnFailure = false;
            LogAssert.Expect(LogType.Error, new Regex(@"GameComposition\.Build: InvalidOperationException: GameComposition test failure after step 5 pause"));
            LogAssert.Expect(LogType.Exception, new Regex("test failure after step 5 pause"));
            var host = new GameObject("CompositionTests host");
            try
            {
                var config = Resources.Load<GameRoot>("GameRoot").Config;
                var e = Assert.Throws<InvalidOperationException>(() =>
                    GameComposition.Build(host.transform, config, new SavePaths(Path.Combine(tempRoot, "saves")), new SettingsPaths(Path.Combine(tempRoot, "settings.json"))));
                StringAssert.Contains("5 pause", e.Message);
                CollectionAssert.AreEqual(new[] { "PauseService", "AudioService", "InputService", "RegistryLifetime", "BlueprintLibrary", "RenderConfigLifetime", "ContentService", "SettingsService" }, GameComposition.LastDisposeLog,
                    "everything built before the failure is disposed, newest first");
                Assert.AreEqual(0, Game.Core.Services.Count);
            }
            finally
            {
                Object.Destroy(host);
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);   // its content singletons point at disposed blobs
                world.Dispose();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator BootSingletons_Tagged()
        {
            yield return null;
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<BlockDefsRef>());
            using var entities = q.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(1, entities.Length, "content singletons exist");
            foreach (var e in entities) Assert.IsTrue(em.HasComponent<BootSingleton>(e), "boot singletons carry BootSingleton");
            Assert.IsNotNull(GameRoot.Instance.Composition.GameConfig, "config_game resolved");
            using var registryQuery = em.CreateEntityQuery(typeof(Game.Voxel.Model.VoxelStructureRegistryRef), typeof(BootSingleton));
            Assert.AreEqual(1, registryQuery.CalculateEntityCount());
            Assert.AreSame(GameRoot.Instance.Composition.Registry, registryQuery.GetSingleton<Game.Voxel.Model.VoxelStructureRegistryRef>().Value.Value);
            Assert.AreSame(GameRoot.Instance.Composition.Registry, Game.Core.Services.Get<Game.Voxel.Grid.VoxelStructureRegistry>());
            Assert.AreSame(GameRoot.Instance.Composition.Blueprints, Game.Core.Services.Get<Game.Voxel.Grid.BlueprintLibrary>());
        }

        [UnityTest]
        public IEnumerator TestHooks_RedirectPaths()
        {
            yield return Shutdown();
            var before = Snapshot(Application.persistentDataPath);
            tempRoot = Path.Combine(Application.temporaryCachePath, "CompositionTests_" + Guid.NewGuid().ToString("N"));
            GameRootTestHooks.SaveRoot = Path.Combine(tempRoot, "saves");
            GameRootTestHooks.SettingsPath = Path.Combine(tempRoot, "settings.json");
            Bootstrap.Boot();
            yield return null;

            var settings = Game.Core.Services.Get<ISettingsService>();
            var edited = settings.CreateEditableCopy();
            edited.Audio.MasterVolume = settings.Current.Audio.MasterVolume > 0.5f ? 0.25f : 0.75f;
            settings.Apply(edited, SettingsSection.Audio);
            settings.Flush();
            Game.Core.Services.Get<SaveService>().CreateProfile("Hook test");
            yield return null;

            Assert.IsTrue(File.Exists(GameRootTestHooks.SettingsPath), "settings written to the hooked path");
            Assert.IsTrue(Directory.Exists(Path.Combine(GameRootTestHooks.SaveRoot, "p001")), "profile written under the hooked save root");
            CollectionAssert.AreEquivalent(before, Snapshot(Application.persistentDataPath), "nothing written under persistentDataPath");
        }

        static List<string> Snapshot(string dir)
        {
            var list = new List<string>();
            if (!Directory.Exists(dir)) return list;
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
            {
                var name = Path.GetFileName(f);
                if (name == "TestResults.xml" || name == "PerformanceTestResults.json") continue;   // the test runner's own files
                list.Add(f + "@" + File.GetLastWriteTimeUtc(f).Ticks);
            }
            return list;
        }
    }
}
