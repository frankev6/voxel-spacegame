using System;
using System.Collections.Generic;
using Game.Core;
using Game.App.Session;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Persistence.Services;
using Game.UI.Services;
using Game.UI.Views;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.App.Flow
{
    /// <summary>The only scene loader (SPEC-01 decision 4). Main thread. Serializes transitions, creates and destroys GameplayActive,
    /// SimClock and WorldSceneActive, runs the ready and exiting hooks, tears down every entity created after boot, and recovers to the
    /// main menu on failure. Implements SPEC-01 §4.3, §5.4.</summary>
    public sealed class SceneFlow : IDisposable
    {
        const int MaxCleanupFrames = 10;
        GameSession session;
        internal void BindSession(GameSession value) => session = value;
        internal PauseToken PauseForExitSave() => pause.Acquire("SceneFlow exit save");

        readonly World world;
        readonly IPauseService pause;
        readonly SaveService save;
        readonly GameConfigDefinition gameConfig;
        readonly List<ISceneReady> readyHooks = new List<ISceneReady>();
        readonly List<ISceneExiting> exitingHooks = new List<ISceneExiting>();
        NativeHashSet<Entity> bootEntities;
        PauseToken pauseToken;
        IRootScreenSet rootScreens;
        UINavigator navigator;
        bool disposed;

        public GameScene CurrentScene { get; private set; }
        public SceneFlowState State { get; private set; }
        public bool IsTransitioning => State != SceneFlowState.Idle;
        /// <summary>0..1 for LoadingView.</summary>
        public float Progress { get; private set; }

        /// <summary>Total time allowed for all ready hooks of one entry (unscaled). From config_game; tests shorten it.</summary>
        internal float ReadyTimeoutSeconds { get; set; }

        public SceneFlow(World world, IPauseService pause, SaveService save, GameConfigDefinition gameConfig)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.pause = pause ?? throw new ArgumentNullException(nameof(pause));
            this.save = save;
            this.gameConfig = gameConfig;
            ReadyTimeoutSeconds = gameConfig != null ? gameConfig.SceneReadyTimeoutSeconds : 30f;
        }

        EntityManager Em => world.EntityManager;

        /// <summary>Sorted by SceneReadyOrder, then type full name.</summary>
        public void RegisterReady(ISceneReady hook)
        {
            if (hook == null) throw new ArgumentNullException(nameof(hook));
            readyHooks.Add(hook);
            readyHooks.Sort((a, b) => a.SceneReadyOrder != b.SceneReadyOrder ? a.SceneReadyOrder.CompareTo(b.SceneReadyOrder)
                                                                            : string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName));
        }

        /// <summary>Sorted by SceneExitOrder, then type full name.</summary>
        public void RegisterExiting(ISceneExiting hook)
        {
            if (hook == null) throw new ArgumentNullException(nameof(hook));
            exitingHooks.Add(hook);
            exitingHooks.Sort((a, b) => a.SceneExitOrder != b.SceneExitOrder ? a.SceneExitOrder.CompareTo(b.SceneExitOrder)
                                                                              : string.CompareOrdinal(a.GetType().FullName, b.GetType().FullName));
        }

        internal bool UnregisterReady(ISceneReady hook) => readyHooks.Remove(hook);
        internal bool UnregisterExiting(ISceneExiting hook) => exitingHooks.Remove(hook);

        /// <summary>Installs the app-lifetime roots after UI registration. Null detaches UI during disposal or headless flow.</summary>
        public void SetRootScreens(IRootScreenSet screens)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SceneFlow));
            var nextNavigator = screens == null ? null : Game.Core.Services.Get<UINavigator>();
            rootScreens = screens;
            navigator = nextNavigator;
        }

        void ShowSceneRoot(GameScene scene)
        {
            if (rootScreens == null) return;
            switch (scene)
            {
                case GameScene.MainMenu: navigator.SetRoot(rootScreens.MainMenu); break;
                case GameScene.World: navigator.SetRoot(rootScreens.FlightHud); break;
                case GameScene.BlueprintLab: navigator.SetRoot(rootScreens.ShipEditor); break;
            }
        }

        void ShowLoadingRoot()
        {
            if (rootScreens != null) navigator.SetRoot(rootScreens.Loading);
        }

        /// <summary>§5.4.5: every entity that exists at the end of boot (config and system-owned singletons) survives teardown.</summary>
        public void SnapshotBootEntities()
        {
            if (bootEntities.IsCreated) bootEntities.Dispose();
            world.SetTime(world.Time);            // Entities creates its WorldTime singleton lazily in SetTime: make it part of the boot set
            using var all = Em.GetAllEntities(Allocator.Temp);
            bootEntities = new NativeHashSet<Entity>(Math.Max(16, all.Length), Allocator.Persistent);
            foreach (var e in all) bootEntities.Add(e);
        }

        internal int BootEntityCount => bootEntities.IsCreated ? bootEntities.Count : 0;

        /// <summary>§5.4.6, once from GameRoot.Start. Scenes that are not one of the four (test and look-dev scenes) are left alone
        /// (SPEC-01 open question 9).</summary>
        public async Awaitable<FlowResult> StartupAsync()
        {
            var active = GameSceneNames.FromSceneName(SceneManager.GetActiveScene().name);
            switch (active)
            {
                case GameScene.Boot:
                    return await TransitionAsync(GameScene.MainMenu, SceneEntryReason.Startup);
                case GameScene.MainMenu:
                    return await EnterInPlaceAsync(GameScene.MainMenu, SceneEntryReason.Startup);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                case GameScene.World:
                    if (session != null) return await session.DevQuickStartAsync();
                    return await TransitionAsync(GameScene.World, SceneEntryReason.DevQuickStart);
                case GameScene.BlueprintLab:
                    // TODO(FND-013): EnterBlueprintLabAsync(new BlueprintLabArgs(Creative, 0, ContentId.None)) with a BeginSandbox(1) session.
                    return await TransitionAsync(GameScene.BlueprintLab, SceneEntryReason.DevQuickStart);
#else
                case GameScene.World:
                case GameScene.BlueprintLab:
                    return await TransitionAsync(GameScene.MainMenu, SceneEntryReason.Startup);
#endif
                default:
                    return FlowResult.Ok;
            }
        }

        /// <summary>Dev-only creative World without galaxy streaming (SPEC-01 §5.4.4).</summary>
        public Awaitable<FlowResult> EnterSandboxAsync(SandboxArgs args)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!disposed && session != null) return session.EnterSandboxAsync(args);
#endif
            var completed = new AwaitableCompletionSource<FlowResult>();
            completed.SetResult(FlowResult.NotAllowed);
            return completed.Awaitable;
        }

        /// <summary>§5.4.1. New game, load, quit and the lab go through GameSession (IGameFlow), which calls this.</summary>
        internal async Awaitable<FlowResult> TransitionAsync(GameScene target, SceneEntryReason reason, BlueprintLabArgs lab = default, Func<Awaitable> betweenScenes = null)
        {
            // 1.
            if (State != SceneFlowState.Idle) return FlowResult.Busy;
            State = SceneFlowState.Exiting;
            Progress = 0f;
            EventBus<SceneTransitionStarted>.Publish(new SceneTransitionStarted(CurrentScene, target, reason));
            pauseToken = pause.Acquire("SceneFlow");

            try
            {
                ShowLoadingRoot();
                // 2. Exit the current scene.
                if (CurrentScene == GameScene.World || CurrentScene == GameScene.BlueprintLab)
                {
                    session?.RunWritebacks();
                    // Session flow saves before setting Exiting; its operation reservation rejects competing flow calls.
                    var exitContext = new SceneExitContext(CurrentScene, target, reason);
                    foreach (var hook in exitingHooks.ToArray())
                    {
                        try { hook.OnSceneExiting(exitContext); }
                        catch (Exception e) { GameLog.Exception(LogCategories.Flow, e, "ISceneExiting " + hook.GetType().Name); }
                    }
                    await TeardownAsync();
                }

                // 3.
                State = SceneFlowState.Loading;
                if (betweenScenes != null) await betweenScenes();

                // 4.
                var op = SceneManager.LoadSceneAsync(target.ToSceneName(), LoadSceneMode.Single);
                if (op == null) throw new InvalidOperationException($"scene '{target.ToSceneName()}' could not be loaded (not in the build list?)");
                while (!op.isDone) { Progress = 0.1f + 0.5f * op.progress; await Awaitable.NextFrameAsync(); }
                Progress = 0.6f;

                // 5.
                State = SceneFlowState.Entering;
                CreateSceneEntities(target);
                CurrentScene = target;
                await RunReadyHooksAsync(new SceneReadyContext(target, reason, lab));
                ShowSceneRoot(target);
            }
            catch (Exception e)
            {
                return await RecoverAsync(e);
            }

            // 6.
            Finish(target, reason, true);
            if (target == GameScene.World && (reason == SceneEntryReason.NewGame || reason == SceneEntryReason.LoadGame)) save?.Autosave.NotifySaved();
            return FlowResult.Ok;
        }

        /// <summary>§5.4.6 MainMenu at startup: treat the loaded scene as entered without reloading it.</summary>
        async Awaitable<FlowResult> EnterInPlaceAsync(GameScene scene, SceneEntryReason reason)
        {
            if (State != SceneFlowState.Idle) return FlowResult.Busy;
            State = SceneFlowState.Entering;
            pauseToken = pause.Acquire("SceneFlow");
            EventBus<SceneTransitionStarted>.Publish(new SceneTransitionStarted(GameScene.None, scene, reason));
            try
            {
                ShowLoadingRoot();
                CurrentScene = scene;
                await RunReadyHooksAsync(new SceneReadyContext(scene, reason, default));
                ShowSceneRoot(scene);
            }
            catch (Exception e) { return await RecoverAsync(e); }
            Finish(scene, reason, true);
            return FlowResult.Ok;
        }

        void Finish(GameScene scene, SceneEntryReason reason, bool success)
        {
            pauseToken.Dispose();
            pauseToken = default;
            Progress = 1f;
            State = SceneFlowState.Idle;
            CurrentScene = scene;
            EventBus<SceneTransitionFinished>.Publish(new SceneTransitionFinished(scene, reason, success));
        }

        /// <summary>World: GameplayActive + SimClock + WorldSceneActive on one entity. BlueprintLab: the same without WorldSceneActive,
        /// with a throwaway clock at 0. MainMenu: nothing.</summary>
        void CreateSceneEntities(GameScene target)
        {
            if (target != GameScene.World && target != GameScene.BlueprintLab) return;
            double start = target == GameScene.World && session != null ? session.UniversalTime : 0;
            var e = target == GameScene.World
                ? Em.CreateEntity(typeof(GameplayActive), typeof(SimClock), typeof(WorldSceneActive))
                : Em.CreateEntity(typeof(GameplayActive), typeof(SimClock));
            Em.SetComponentData(e, new SimClock { Tick = 0, UniversalTime = start, SessionStartUniversalTime = start });
#if UNITY_EDITOR
            Em.SetName(e, "GameplayActive");
#endif
        }

        async Awaitable RunReadyHooksAsync(SceneReadyContext context)
        {
            float deadline = Time.realtimeSinceStartup + ReadyTimeoutSeconds;
            var hooks = readyHooks.ToArray();
            for (int i = 0; i < hooks.Length; i++)
            {
                hooks[i].OnSceneReady(context);
                while (!hooks[i].IsSceneReadyComplete(context))
                {
                    if (Time.realtimeSinceStartup > deadline)
                        throw new TimeoutException($"scene ready hook {hooks[i].GetType().Name} did not complete within {ReadyTimeoutSeconds} s");
                    await Awaitable.NextFrameAsync();
                }
                Progress = 0.6f + 0.4f * (i + 1) / hooks.Length;
            }
        }

        /// <summary>§5.4.1 step 7.</summary>
        async Awaitable<FlowResult> RecoverAsync(Exception error)
        {
            State = SceneFlowState.Recovering;
            GameLog.Exception(LogCategories.Flow, error, "SceneFlow transition failed; returning to the main menu");
            try
            {
                await TeardownAsync();
                session?.EndSession();
                var op = SceneManager.LoadSceneAsync(GameScene.MainMenu.ToSceneName(), LoadSceneMode.Single);
                while (op != null && !op.isDone) await Awaitable.NextFrameAsync();
            }
            catch (Exception e) { GameLog.Exception(LogCategories.Flow, e, "SceneFlow recovery"); }
            // A broken view must not retain the transition pause token or prevent the failed result.
            try { ShowSceneRoot(GameScene.MainMenu); }
            catch (Exception e) { GameLog.Exception(LogCategories.Flow, e, "SceneFlow recovery root"); }
            // TODO(UI-003): ui.Notifications.Show("Something went wrong while loading. Returned to the main menu.", Error).
            Finish(GameScene.MainMenu, SceneEntryReason.ErrorRecovery, false);
            return FlowResult.Failed;
        }

        /// <summary>§5.4.1 step 2.4: destroy everything created after boot, then give cleanup systems up to 10 frames.</summary>
        async Awaitable TeardownAsync()
        {
            DestroyNonBootEntities();
            for (int frame = 0; frame < MaxCleanupFrames && CountNonBootEntities() > 0; frame++) await Awaitable.NextFrameAsync();
            if (CountNonBootEntities() > 0)
            {
                GameLog.Error(LogCategories.Flow, "Entities survived teardown for " + MaxCleanupFrames + " frames: " + DescribeNonBootArchetypes(10));
                DestroyNonBootEntities();
            }
        }

        /// <summary>Steps 2.4.1–2.4.3, synchronous. Returns how many entities were destroyed.</summary>
        internal int DestroyNonBootEntities()
        {
            var em = Em;
            em.CompleteAllTrackedJobs();
            using (var q = em.CreateEntityQuery(ComponentType.ReadOnly<GameplayActive>())) em.DestroyEntity(q);
            using var all = em.GetAllEntities(Allocator.Temp);
            var doomed = new NativeList<Entity>(all.Length, Allocator.Temp);
            foreach (var e in all) if (!IsBoot(e)) doomed.Add(e);
            if (doomed.Length > 0) em.DestroyEntity(doomed.AsArray());
            int n = doomed.Length;
            doomed.Dispose();
            // ADR-0045: pause prevents fixed-step cleanup. Let Physics own invalidation and the empty broadphase rebuild.
            var physics = world.GetExistingSystemManaged<PhysicsInitializeGroup>();
            if (physics != null)
            {
                world.PushTime(new Unity.Core.TimeData(world.Time.ElapsedTime, 0));
                try
                {
                    physics.Update();
                    em.CompleteAllTrackedJobs();
                    world.GetExistingSystemManaged<EndFixedStepSimulationEntityCommandBufferSystem>()?.Update();
                    em.CompleteAllTrackedJobs();
                }
                finally { world.PopTime(); }
            }
            return n;
        }

        internal int CountNonBootEntities()
        {
            using var all = Em.GetAllEntities(Allocator.Temp);
            int n = 0;
            foreach (var e in all) if (!IsBoot(e)) n++;
            return n;
        }

        bool IsBoot(Entity e) => bootEntities.IsCreated && bootEntities.Contains(e);

        internal string DescribeNonBootArchetypes(int max)
        {
            var seen = new List<string>();
            using var all = Em.GetAllEntities(Allocator.Temp);
            foreach (var e in all)
            {
                if (IsBoot(e)) continue;
                using var types = Em.GetChunk(e).Archetype.GetComponentTypes(Allocator.Temp);
                var names = new List<string>();
                foreach (var t in types) names.Add(t.GetManagedType()?.Name ?? t.ToString());
                var s = "[" + string.Join(", ", names) + "]";
                if (!seen.Contains(s)) seen.Add(s);
                if (seen.Count >= max) break;
            }
            return string.Join("; ", seen);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            rootScreens = null;
            navigator = null;
            pauseToken.Dispose();
            if (bootEntities.IsCreated) bootEntities.Dispose();
            readyHooks.Clear();
            exitingHooks.Clear();
        }
    }
}
