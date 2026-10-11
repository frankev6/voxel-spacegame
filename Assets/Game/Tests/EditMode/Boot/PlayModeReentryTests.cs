using System.Collections;
using System.Diagnostics;
using Game.App.Boot;
using Game.App.Flow;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Tests.Common.Perf;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Boot
{
    /// <summary>FND-014: with domain reload off, a second Play session starts as clean as the first (SPEC-01 FND-014). Enters Play Mode
    /// from the Boot scene so the real startup path runs: Boot → MainMenu.</summary>
    public sealed class PlayModeReentryTests
    {
        [UnitySetUp]
        public IEnumerator OpenBoot()
        {
            EditorSceneManager.OpenScene("Assets/Game/Scenes/Boot.unity", OpenSceneMode.Single);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Leave()
        {
            if (Application.isPlaying) yield return new ExitPlayMode();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static bool MenuReady()
            => GameRoot.Instance != null && Services.TryGet<SceneFlow>(out var flow) && flow.CurrentScene == GameScene.MainMenu && !flow.IsTransitioning;

        /// <summary>Waits for MainMenu; returns false after 10 s.</summary>
        static IEnumerator WaitForMenu(System.Action<bool> done)
        {
            var sw = Stopwatch.StartNew();
            while (!MenuReady() && sw.Elapsed.TotalSeconds < 10) yield return null;
            done(MenuReady());
        }

        [UnityTest]
        public IEnumerator SecondPlay_Clean()
        {
            bool ready = false;
            yield return new EnterPlayMode();
            yield return WaitForMenu(r => ready = r);
            Assert.IsTrue(ready, "first Play reaches MainMenu");
            for (int i = 0; i < 5; i++) yield return null;
            int services = Services.Count, channels = EventHub.ChannelCount;
            yield return new ExitPlayMode();
            Assert.IsNull(GameRoot.Instance);

            yield return new EnterPlayMode();
            yield return WaitForMenu(r => ready = r);
            Assert.IsTrue(ready, "second Play reaches MainMenu");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(services, Services.Count, "same services as the first Play");
            Assert.AreEqual(channels, EventHub.ChannelCount, "same event channels as the first Play");
            Assert.AreEqual(1, Object.FindObjectsByType<GameRoot>(FindObjectsInactive.Include).Length, "exactly one GameRoot");
            Assert.AreEqual(0, EventHub.PendingCount, "nothing left pending");
        }

        [UnityTest, Performance, Category("Performance")]
        public IEnumerator EnterToMenu_Under1s()
        {
            var ms = new double[3];
            for (int run = 0; run < ms.Length; run++)
            {
                yield return new EnterPlayMode();
                var sw = Stopwatch.StartNew();                        // from the first Play frame to MainMenu ready
                bool ready = false;
                yield return WaitForMenu(r => ready = r);
                ms[run] = sw.Elapsed.TotalMilliseconds;
                Assert.IsTrue(ready);
                yield return new ExitPlayMode();
            }
            var stat = PerfRun.Gate("EnterToMenu", ms, 1000.0);
            UnityEngine.Debug.Log($"Play Mode enter to MainMenu ready: median {stat.Median:F0} ms");
        }
    }
}
