using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.App.Boot;
using Game.App.Flow;
using Game.App.Testing;
using Game.Core;
using Game.Core.Components;
using Game.Core.Flow;
using Game.Input;
using Game.Persistence.Services;
using Game.Simulation.Commands;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.Common
{
    /// <summary>SPEC-71 §5.6: production boot/flow with isolated save and settings folders.</summary>
    public abstract class GameFlowFixture
    {
        readonly Queue<string> recentLogs = new Queue<string>(20);
        NativeLeakDetectionMode previousLeakMode;
        bool ownsSetup;
        internal string TestDirectory { get; private set; }
        /// <summary>QA-001: fixtures that test free flight, menus or saving opt out of automatic story dialogue.</summary>
        protected virtual bool QuietCampaign => false;
        protected EntityManager Em => World.DefaultGameObjectInjectionWorld.EntityManager;
        protected T Service<T>() where T : class => Services.Get<T>();
        protected Entity PlayerRoot
        {
            get
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated) return Entity.Null;
                using var query = Em.CreateEntityQuery(typeof(PlayerShip));
                return query.CalculateEntityCount() == 1 ? query.GetSingletonEntity() : Entity.Null;
            }
        }
        protected bool PlayerReady
        {
            get
            {
                var root = PlayerRoot;
                if (root == Entity.Null || !Em.HasComponent<PhysicsCollider>(root) || !Em.GetComponentData<PhysicsCollider>(root).IsValid) return false;
                using var active = Em.CreateEntityQuery(typeof(GameplayActive));
                return !active.IsEmptyIgnoreFilter && Service<InputService>().CurrentContext == InputContext.Flight;
            }
        }
        [UnitySetUp]
        public IEnumerator GameFlowSetUp()
        {
            ownsSetup = true;
            previousLeakMode = NativeLeakDetection.Mode;
            NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            LogAssert.ignoreFailingMessages = false;
            recentLogs.Clear(); Application.logMessageReceived += CaptureLog;
            yield return LeaveSession();
            if (GameRoot.Instance != null) { Object.Destroy(GameRoot.Instance.gameObject); yield return WaitFrames(2); }
            GameRootTestHooks.Reset();
            TestDirectory = Path.GetFullPath(Path.Combine("Temp", "TestRuns", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(TestDirectory);
            GameRootTestHooks.SaveRoot = Path.Combine(TestDirectory, "saves");
            GameRootTestHooks.SettingsPath = Path.Combine(TestDirectory, "settings.json");
            GameRootTestHooks.SkipIntroScreens = true;
            GameRootTestHooks.SkipCampaignOpening = QuietCampaign;
            yield return SceneManager.LoadSceneAsync("Boot");
            Bootstrap.Boot();
            // Match production readiness on GPU; only null-device functional tests bypass rendering.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                using var query = Em.CreateEntityQuery(typeof(VoxelConfig));
                var config = query.GetSingleton<VoxelConfig>(); config.RequireRenderer = 0; query.SetSingleton(config);
            }
            yield return WaitUntil(() => Services.TryGet<SceneFlow>(out var flow) && !flow.IsTransitioning && flow.CurrentScene == GameScene.MainMenu, 30, "Boot reaches MainMenu");
            var save = Service<SaveService>(); save.SetActiveProfile(save.CreateProfile("Test").Id);
        }
        [UnityTearDown]
        public IEnumerator GameFlowTearDown()
        {
            if (!ownsSetup) yield break;
            try
            {
                yield return LeaveSession();
                if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
                yield return WaitFrames(2);
            }
            finally
            {
                // Also request root disposal if a failed flow interrupted normal teardown.
                if (GameRoot.Instance != null) Object.Destroy(GameRoot.Instance.gameObject);
                GameRootTestHooks.Reset();
                if (TestDirectory != null)
                {
                    try { if (Directory.Exists(TestDirectory)) Directory.Delete(TestDirectory, true); }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    { Debug.LogWarning("Test folder cleanup: " + e.Message); }
                }
                Application.logMessageReceived -= CaptureLog;
                NativeLeakDetection.Mode = previousLeakMode;
                ownsSetup = false;
            }
            LogAssert.NoUnexpectedReceived();
        }
        IEnumerator LeaveSession()
        {
            if (!Services.TryGet<SceneFlow>(out var scene)) yield break;
            yield return WaitUntil(() => !scene.IsTransitioning, 30, "Existing transition settles");
            if (Services.TryGet<IGameFlow>(out var flow) && flow.HasActiveSession)
                yield return Complete(flow.QuitToMainMenuAsync(false), "Quit to MainMenu");
            else if (scene.CurrentScene == GameScene.World || scene.CurrentScene == GameScene.BlueprintLab)
                yield return Complete(scene.TransitionAsync(GameScene.MainMenu, SceneEntryReason.Startup), "Return to MainMenu");
        }
        protected IEnumerator NewGame(ulong seed)
        {
            yield return Complete(Service<IGameFlow>().NewGameAsync(new NewGameArgs("Test", seed, false, ContentId.None)), "New game");
            yield return WaitUntil(() => PlayerReady, 60, "New game player ready");
        }
        protected IEnumerator EnterSandbox()
        {
            yield return Complete(Service<SceneFlow>().EnterSandboxAsync(new SandboxArgs(true, 42)), "Enter sandbox");
            yield return WaitUntil(() => PlayerReady, 60, "Sandbox player ready");
        }
        protected IEnumerator Complete(Awaitable<FlowResult> operation, string what)
        {
            var awaiter = operation.GetAwaiter();
            yield return WaitUntil(() => awaiter.IsCompleted, 60, what);
            Assert.AreEqual(FlowResult.Ok, awaiter.GetResult(), what);
        }
        protected IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble >= deadline)
                {
                    string state = Services.TryGet<SceneFlow>(out var flow) ? flow.CurrentScene + "/" + flow.State : "unregistered";
                    Assert.Fail(what + "; SceneFlow=" + state + "\n" + string.Join("\n", recentLogs));
                }
                yield return null;
            }
        }
        protected IEnumerator WaitFrames(int frames) { for (int i = 0; i < frames; i++) yield return null; }
        protected IEnumerator WaitTicks(int ticks)
        {
            if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
            using var clock = Em.CreateEntityQuery(typeof(SimClock));
            uint start = clock.GetSingleton<SimClock>().Tick;
            yield return WaitUntil(() => clock.GetSingleton<SimClock>().Tick - start >= (uint)ticks, System.Math.Max(10, ticks / 60f + 10), "Simulation ticks advance");
        }
        protected PauseToken HoldSimulation(string reason) => Service<IPauseService>().Acquire(reason);
        void CaptureLog(string message, string trace, LogType type)
        {
            if (recentLogs.Count == 20) recentLogs.Dequeue();
            recentLogs.Enqueue(type + ": " + message);
        }
    }
}
