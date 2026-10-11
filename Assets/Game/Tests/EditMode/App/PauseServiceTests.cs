using System.Collections.Generic;
using Game.App.Services;
using Game.Core;
using Game.Core.Events;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.App
{
    /// <summary>FND-003: PauseService (SPEC-01 §5.6).</summary>
    public sealed class PauseServiceTests
    {
        readonly List<PauseStateChanged> events = new List<PauseStateChanged>();
        void OnChanged(PauseStateChanged e) => events.Add(e);

        [SetUp] public void SetUp() { EventHub.Reset(); events.Clear(); EventBus<PauseStateChanged>.Subscribe(OnChanged); Time.timeScale = 1f; }   // conventions-ok: test setup
        [TearDown] public void TearDown() { EventHub.Reset(); Time.timeScale = 1f; }   // conventions-ok: test teardown

        [Test]
        public void RefCounted()
        {
            using var pause = new PauseService();
            var a = pause.Acquire("UINavigator");
            var b = pause.Acquire("SceneFlow");
            Assert.IsTrue(pause.IsPaused); Assert.AreEqual(2, pause.ActiveTokenCount); Assert.AreEqual(0f, Time.timeScale);
            var reasons = new List<string> { "stale" };
            pause.GetActiveReasons(reasons);
            CollectionAssert.AreEqual(new[] { "UINavigator", "SceneFlow" }, reasons);
            a.Dispose();
            Assert.IsTrue(pause.IsPaused, "still one token"); Assert.AreEqual(0f, Time.timeScale);
            pause.Release(b);
            Assert.IsFalse(pause.IsPaused); Assert.AreEqual(1f, Time.timeScale);
            Assert.Throws<System.ArgumentException>(() => pause.Acquire(""));
        }

        [Test]
        public void DoubleRelease_NoOp()
        {
            using var pause = new PauseService();
            var a = pause.Acquire("a"); var b = pause.Acquire("b");
            a.Dispose(); a.Dispose(); pause.Release(a);
            Assert.AreEqual(1, pause.ActiveTokenCount);
            pause.Release(default);
            using var other = new PauseService();
            pause.Release(new PauseToken(other, b.Id));
            Assert.AreEqual(1, pause.ActiveTokenCount, "a token of another service is ignored");
        }

        [Test]
        public void PublishesOnTransitionsOnly()
        {
            using var pause = new PauseService();
            var a = pause.Acquire("a"); var b = pause.Acquire("b"); a.Dispose(); b.Dispose();
            var c = pause.Acquire("c"); c.Dispose();
            EventHub.DrainAll();
            Assert.AreEqual(4, events.Count);
            Assert.IsTrue(events[0].IsPaused); Assert.AreEqual(1, events[0].TokenCount);
            Assert.IsFalse(events[1].IsPaused); Assert.AreEqual(0, events[1].TokenCount);
            Assert.IsTrue(events[2].IsPaused); Assert.IsFalse(events[3].IsPaused);
        }

        [Test]
        public void Dispose_RestoresTimeScale()
        {
            var pause = new PauseService();
            pause.Acquire("a"); pause.Acquire("b");
            Assert.AreEqual(0f, Time.timeScale);
            pause.Dispose();
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(pause.IsPaused);
            EventHub.DrainAll();
            Assert.AreEqual(2, events.Count, "pause, then the unpause from Dispose");
        }
    }
}
