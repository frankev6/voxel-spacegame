using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.App.Flow;
using Game.Core;
using Game.Core.Components;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Boot
{
    /// <summary>FND-006: SceneFlow transitions, teardown and recovery (SPEC-01 §5.4).</summary>
    public sealed class SceneFlowTests
    {
        public struct LeftoverA : IComponentData { public int V; }
        public struct LeftoverB : IComponentData { public float3 P; }
        [InternalBufferCapacity(2)] public struct LeftoverBuffer : IBufferElementData { public int V; }
        public struct LeftoverTag : IComponentData { }

        sealed class ThrowingHook : ISceneReady
        {
            public int SceneReadyOrder => SceneHookOrder.Default;
            public void OnSceneReady(in SceneReadyContext context) { if (context.Scene == GameScene.World) throw new InvalidOperationException("hook failure for the test"); }
            public bool IsSceneReadyComplete(in SceneReadyContext context) => true;
        }

        sealed class NeverReadyHook : ISceneReady
        {
            public int SceneReadyOrder => SceneHookOrder.Default;
            public void OnSceneReady(in SceneReadyContext context) { }
            public bool IsSceneReadyComplete(in SceneReadyContext context) => context.Scene != GameScene.World;
        }

        sealed class RecordingHooks : ISceneReady, ISceneExiting
        {
            public readonly List<string> Log = new List<string>();
            public int SceneReadyOrder => SceneHookOrder.Default;
            public int SceneExitOrder => SceneHookOrder.Default;
            public void OnSceneReady(in SceneReadyContext context) => Log.Add("ready " + context.Scene + " " + context.Reason);
            public bool IsSceneReadyComplete(in SceneReadyContext context) => true;
            public void OnSceneExiting(in SceneExitContext context) => Log.Add("exit " + context.Scene + " → " + context.NextScene);
        }

        static SceneFlow Flow => Services.Get<SceneFlow>();
        static EntityManager Em => World.DefaultGameObjectInjectionWorld.EntityManager;

        /// <summary>Drives an Awaitable from a coroutine test.</summary>
        sealed class Run
        {
            public FlowResult Result;
            public IEnumerator Wait(Awaitable<FlowResult> op)
            {
                var awaiter = op.GetAwaiter();
                float timeout = Time.realtimeSinceStartup + 30f;
                while (!awaiter.IsCompleted)
                {
                    if (Time.realtimeSinceStartup > timeout) Assert.Fail("transition did not finish within 30 s");
                    yield return null;
                }
                Result = awaiter.GetResult();
            }
        }

        static IEnumerator Go(GameScene target, SceneEntryReason reason, FlowResult expected = FlowResult.Ok)
        {
            var run = new Run();
            yield return run.Wait(Flow.TransitionAsync(target, reason));
            Assert.AreEqual(expected, run.Result, $"transition to {target}");
        }

        static int GameplayActiveCount()
        {
            using var q = Em.CreateEntityQuery(ComponentType.ReadOnly<GameplayActive>());
            return q.CalculateEntityCount();
        }

        static void SpawnLeftovers(int n)
        {
            var em = Em;
            using var a = em.CreateEntity(em.CreateArchetype(typeof(LeftoverA)), n, Allocator.Temp);
            using var b = em.CreateEntity(em.CreateArchetype(typeof(LeftoverB), typeof(LeftoverBuffer)), n, Allocator.Temp);
            using var c = em.CreateEntity(em.CreateArchetype(typeof(LeftoverTag), typeof(LeftoverA), typeof(Disabled)), n, Allocator.Temp);
            var prefab = em.CreateEntity(typeof(LeftoverA), typeof(Prefab));
            em.Instantiate(prefab);
        }

        [UnityTest]
        public IEnumerator RoundTrip_NoLeftovers()
        {
            var hooks = new RecordingHooks();
            Flow.RegisterReady(hooks); Flow.RegisterExiting(hooks);
            try
            {
                yield return Go(GameScene.MainMenu, SceneEntryReason.QuitToMenu);
                Assert.AreEqual(0, GameplayActiveCount());
                for (int round = 0; round < 2; round++)
                {
                    yield return Go(GameScene.World, SceneEntryReason.NewGame);
                    Assert.AreEqual("World", SceneManager.GetActiveScene().name);
                    Assert.AreEqual(GameScene.World, Flow.CurrentScene);
                    Assert.AreEqual(1, GameplayActiveCount(), "GameplayActive exists in the World");
                    using (var q = Em.CreateEntityQuery(typeof(GameplayActive), typeof(SimClock), typeof(WorldSceneActive))) Assert.AreEqual(1, q.CalculateEntityCount(), "one entity with all three");
                    SpawnLeftovers(500);
                    yield return null;

                    yield return Go(GameScene.MainMenu, SceneEntryReason.QuitToMenu);
                    Assert.AreEqual(GameScene.MainMenu, Flow.CurrentScene);
                    Assert.AreEqual(0, GameplayActiveCount(), "GameplayActive only in the World");
                    Assert.AreEqual(0, Flow.CountNonBootEntities(), "no entity outside the boot set remains: " + Flow.DescribeNonBootArchetypes(10));
                    using (var q = new EntityQueryBuilder(Allocator.Temp).WithAny<LeftoverA, LeftoverB, LeftoverTag>()
                               .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities).Build(Em))
                        Assert.AreEqual(0, q.CalculateEntityCount(), "prefab and disabled leftovers are gone too");
                }
                CollectionAssert.AreEqual(new[]
                {
                    "ready MainMenu QuitToMenu",
                    "ready World NewGame", "exit World → MainMenu", "ready MainMenu QuitToMenu",
                    "ready World NewGame", "exit World → MainMenu", "ready MainMenu QuitToMenu",
                }, hooks.Log);
                Assert.AreEqual(1f, Time.timeScale, "the transition pause token is released");
            }
            finally { Flow.UnregisterReady(hooks); Flow.UnregisterExiting(hooks); }
        }

        [UnityTest]
        public IEnumerator BlueprintLab_HasGameplayActiveWithoutWorldScene()
        {
            yield return Go(GameScene.BlueprintLab, SceneEntryReason.EnterBlueprintLab);
            Assert.AreEqual(1, GameplayActiveCount());
            using (var q = Em.CreateEntityQuery(ComponentType.ReadOnly<WorldSceneActive>())) Assert.AreEqual(0, q.CalculateEntityCount());
            using (var q = Em.CreateEntityQuery(ComponentType.ReadOnly<SimClock>())) Assert.AreEqual(0u, q.GetSingleton<SimClock>().Tick);
            yield return Go(GameScene.MainMenu, SceneEntryReason.ExitBlueprintLab);
            Assert.AreEqual(0, GameplayActiveCount());
        }

        [UnityTest]
        public IEnumerator Busy_DuringTransition()
        {
            var first = new Run();
            var op = Flow.TransitionAsync(GameScene.World, SceneEntryReason.NewGame);
            Assert.IsTrue(Flow.IsTransitioning);
            var second = Flow.TransitionAsync(GameScene.MainMenu, SceneEntryReason.QuitToMenu).GetAwaiter();
            Assert.IsTrue(second.IsCompleted, "a second request answers at once");
            Assert.AreEqual(FlowResult.Busy, second.GetResult());
            yield return first.Wait(op);
            Assert.AreEqual(FlowResult.Ok, first.Result);
            yield return Go(GameScene.MainMenu, SceneEntryReason.QuitToMenu);
        }

        [UnityTest]
        public IEnumerator ThrowingHook_Recovers()
        {
            var finished = new List<SceneTransitionFinished>();
            Action<SceneTransitionFinished> onFinished = e => finished.Add(e);
            EventBus<SceneTransitionFinished>.Subscribe(onFinished);
            var hook = new ThrowingHook();
            Flow.RegisterReady(hook);
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("SceneFlow transition failed.*hook failure for the test"));
                LogAssert.Expect(LogType.Exception, new Regex("hook failure for the test"));
                yield return Go(GameScene.World, SceneEntryReason.NewGame, FlowResult.Failed);
                yield return null;                                     // EventHub drains at the end of the frame
                Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
                Assert.AreEqual(GameScene.MainMenu, Flow.CurrentScene);
                Assert.AreEqual(SceneFlowState.Idle, Flow.State);
                Assert.AreEqual(0, GameplayActiveCount(), "recovery tears the World down");
                Assert.AreEqual(0, Flow.CountNonBootEntities(), Flow.DescribeNonBootArchetypes(10));
                var last = finished[finished.Count - 1];
                Assert.AreEqual(GameScene.MainMenu, last.Scene); Assert.AreEqual(SceneEntryReason.ErrorRecovery, last.Reason); Assert.IsFalse(last.Success);
                Assert.AreEqual(1f, Time.timeScale);
            }
            finally { Flow.UnregisterReady(hook); EventBus<SceneTransitionFinished>.Unsubscribe(onFinished); }
        }

        [UnityTest]
        public IEnumerator TimeoutHook_Recovers()
        {
            var hook = new NeverReadyHook();
            Flow.RegisterReady(hook);
            float saved = Flow.ReadyTimeoutSeconds;
            Flow.ReadyTimeoutSeconds = 1f;
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("SceneFlow transition failed.*TimeoutException.*NeverReadyHook"));
                LogAssert.Expect(LogType.Exception, new Regex("NeverReadyHook did not complete within 1 s"));
                float start = Time.realtimeSinceStartup;
                yield return Go(GameScene.World, SceneEntryReason.NewGame, FlowResult.Failed);
                Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, 1f, "waited for the timeout");
                Assert.AreEqual(GameScene.MainMenu, Flow.CurrentScene);
                Assert.AreEqual(0, GameplayActiveCount());
            }
            finally { Flow.UnregisterReady(hook); Flow.ReadyTimeoutSeconds = saved; }
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator Teardown_200k()
        {
            yield return Go(GameScene.World, SceneEntryReason.NewGame);
            var ms = new double[5];
            for (int i = 0; i < ms.Length; i++)
            {
                var em = Em;
                em.CreateEntity(em.CreateArchetype(typeof(LeftoverA), typeof(LeftoverB)), 150_000, Allocator.Temp).Dispose();
                em.CreateEntity(em.CreateArchetype(typeof(LeftoverTag), typeof(LeftoverBuffer)), 50_000, Allocator.Temp).Dispose();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int destroyed = Flow.DestroyNonBootEntities();
                ms[i] = sw.Elapsed.TotalMilliseconds;
                Assert.GreaterOrEqual(destroyed, 200_000);
                Assert.AreEqual(0, Flow.CountNonBootEntities());
                em.CreateEntity(typeof(GameplayActive), typeof(SimClock), typeof(WorldSceneActive));   // as the World entry made it
                yield return null;
            }
            var stat = PerfRun.Gate("Teardown200k", ms, 50.0);
            Debug.Log($"SceneFlow teardown of 200,000 entities: median {stat.Median:F2} ms");
            yield return Go(GameScene.MainMenu, SceneEntryReason.QuitToMenu);
        }
    }
}
