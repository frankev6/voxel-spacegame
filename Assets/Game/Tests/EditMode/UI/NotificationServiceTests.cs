using System;
using System.Collections.Generic;
using Game.UI.Data;
using Game.UI.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.EditMode.UI
{
    /// <summary>SPEC-60 UI-010: detached production template/pool, controlled unscaled clock and bounded queue.</summary>
    public sealed class NotificationServiceTests
    {
        sealed class Clock : IFrameClock
        {
            public int FrameCount { get; set; }
            public float UnscaledTime { get; set; }
        }
        UIConfig config;
        Clock clock;
        VisualElement layer, column;
        NotificationService service;
        [SetUp] public void SetUp()
        {
            config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
            Assert.IsNotNull(config); Assert.IsNotNull(config.ToastUxml);
            clock = new Clock(); layer = new VisualElement { name = "layer-toasts" };
            Build();
        }
        void Build()
        {
            service = new NotificationService(layer, config, clock);
            column = layer.Q<VisualElement>("toast-column");
            Assert.IsNotNull(column); Assert.AreEqual(config.ToastPoolSize, column.childCount);
        }
        [TearDown] public void TearDown() { service.Dispose(); Object.DestroyImmediate(config); }
        void Post(string label, NotificationKind kind = NotificationKind.Info, int amount = 0, string key = null)
            => service.Post(new Notification { Label = label, Kind = kind, Amount = amount, CoalesceKey = key });
        void Step(float seconds, bool reduceMotion = false)
        { clock.UnscaledTime += seconds; clock.FrameCount++; service.Tick(seconds, reduceMotion); }
        List<VisualElement> VisibleElements()
        {
            var result = new List<VisualElement>();
            for (int i = 0; i < column.childCount; i++)
                if (!column[i].ClassListContains(UIStyleClasses.IsHidden)) result.Add(column[i]);
            return result;
        }
        static Label Text(VisualElement toast) => toast.Q<Label>(toast.name + "-text");
        List<string> VisibleTexts()
        {
            var result = new List<string>();
            foreach (var toast in VisibleElements()) result.Add(Text(toast).text);
            return result;
        }
        [Test] public void Post_SameKeyInWindow_Coalesces()
        {
            for (int i = 0; i < 50; i++) Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            Assert.AreEqual(1, service.VisibleCount); Assert.AreEqual(0, service.QueuedCount);
            var toast = VisibleElements()[0]; Assert.AreEqual("+50 Hull", Text(toast).text);
            Assert.IsTrue(toast.ClassListContains(UIStyleClasses.ToastPositive));
            Assert.IsTrue(toast.ClassListContains(UIStyleClasses.ToastBump));
            Assert.AreEqual("✓", toast.Q<Label>(toast.name + "-icon").text);
            Step(.1f); Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastBump));
            clock.UnscaledTime = 2.49f; Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            clock.UnscaledTime = 4.98f; Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            Assert.AreEqual(1, service.VisibleCount, "Coalescing window slides with the latest bump");
            Assert.AreEqual("+52 Hull", Text(toast).text);
            Step(3.99f); Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastExit), "A bump resets lifetime");
            Step(.01f); Assert.IsTrue(toast.ClassListContains(UIStyleClasses.ToastExit));
        }
        [Test] public void Post_AfterWindow_NewToast()
        {
            Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            clock.UnscaledTime = 2.5f; Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            Assert.AreEqual(2, service.VisibleCount, "Boundary is strictly less than 2.5 seconds");
            CollectionAssert.AreEqual(new[] { "+1 Hull", "+1 Hull" }, VisibleTexts());
            Post("No key"); Post("Empty key", key: "");
            Assert.AreEqual(4, service.VisibleCount, "Empty keys do not coalesce");
        }
        [Test] public void Post_FifthToast_Queued()
        {
            for (int i = 0; i < 4; i++) Post("Visible " + i);
            Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            Assert.AreEqual(4, service.VisibleCount); Assert.AreEqual(1, service.QueuedCount, "Queued requests coalesce in place");
            clock.UnscaledTime = 2.5f; Post("Hull", NotificationKind.Positive, 1, "item:block_hull");
            Assert.AreEqual(2, service.QueuedCount, "Queued requests obey the same timestamp window");
            Step(4.21f); Assert.AreEqual(2, service.VisibleCount); Assert.AreEqual(0, service.QueuedCount);
            CollectionAssert.AreEqual(new[] { "+2 Hull", "+1 Hull" }, VisibleTexts());
        }
        [Test] public void QueueFull_DropsOldestInfoFirst()
        {
            service.Dispose(); config.MaxQueuedToasts = 3; Build();
            for (int i = 0; i < 4; i++) Post("Visible " + i);
            Post("Danger first", NotificationKind.Danger); Post("Info", NotificationKind.Info); Post("Positive", NotificationKind.Positive);
            Post("Danger last", NotificationKind.Danger);
            Assert.AreEqual(3, service.QueuedCount); Step(4.21f);
            CollectionAssert.AreEqual(new[] { "Danger first", "Positive", "Danger last" }, VisibleTexts(), "Drop the oldest Info/Positive while retaining survivor order");
            service.ClearAll();
            for (int i = 0; i < 4; i++) Post("Visible " + i);
            Post("Danger 0", NotificationKind.Danger); Post("Warning", NotificationKind.Warning); Post("Danger 1", NotificationKind.Danger);
            Post("Danger 2", NotificationKind.Danger); Step(4.21f);
            CollectionAssert.AreEqual(new[] { "Warning", "Danger 1", "Danger 2" }, VisibleTexts(), "Without low-priority entries drop the oldest request");
        }
        [Test] public void Tick_Lifetime_ExpiresAndDequeues()
        {
            for (int i = 0; i < 5; i++) Post("Toast " + i);
            var originals = VisibleElements(); Step(0);
            foreach (var toast in originals) Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastEnter), "Enter clears on the next frame");
            Step(3.99f); Assert.AreEqual(4, service.VisibleCount); Assert.AreEqual(1, service.QueuedCount);
            Step(.01f);
            foreach (var toast in originals) Assert.IsTrue(toast.ClassListContains(UIStyleClasses.ToastExit));
            Assert.AreEqual(4, service.VisibleCount, "Exiting toasts keep their capacity slot until the fade finishes");
            Step(.19f); Assert.AreEqual(4, service.VisibleCount);
            Step(.02f); Assert.AreEqual(1, service.VisibleCount); Assert.AreEqual(0, service.QueuedCount);
            var replacement = VisibleElements()[0]; Assert.Contains(replacement, originals, "Reuse a pooled element");
            Assert.AreEqual("Toast 4", Text(replacement).text); Assert.AreEqual(8, column.childCount);
            Step(3.99f); Assert.IsFalse(replacement.ClassListContains(UIStyleClasses.ToastExit), "Dequeued requests receive a full lifetime");
            Step(.01f, reduceMotion: true); Assert.AreEqual(0, service.VisibleCount, "Reduced motion removes the exit delay");
            for (int i = 0; i < 6; i++) Post("Reset " + i);
            Assert.AreEqual(4, service.VisibleCount); Assert.AreEqual(2, service.QueuedCount); service.ClearAll();
            Assert.AreEqual(0, service.VisibleCount); Assert.AreEqual(0, service.QueuedCount);
            foreach (var toast in column.Children())
            {
                Assert.IsTrue(toast.ClassListContains(UIStyleClasses.IsHidden));
                Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastEnter));
                Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastExit));
                Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastBump));
            }
        }
        [Test] public void Tick_Idle_ZeroAlloc()
        {
            Post("Idle"); Step(0);
            for (int i = 0; i < 100; i++) service.Tick(0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) service.Tick(0);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "Visible, unchanged toast ticks do not allocate");
            service.ClearAll();
            for (int i = 0; i < 100; i++) service.Tick(.01f);
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) service.Tick(.01f);
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated, "Empty framework ticks do not allocate");
        }
        [Test] public void NegativeAmount_UsesMinusSign()
        {
            Post("Hull", NotificationKind.Warning, -3);
            Post("Hull", NotificationKind.Danger, int.MinValue);
            CollectionAssert.AreEqual(new[] { "−3 Hull", "−2147483648 Hull" }, VisibleTexts());
            Post("Hull", amount: int.MaxValue, key: "saturated"); Post("Hull", amount: 1, key: "saturated");
            Assert.AreEqual("+2147483647 Hull", Text(VisibleElements()[2]).text, "Coalesced int amounts saturate without wrapping sign");
            Assert.AreEqual("✕", VisibleElements()[1].Q<Label>(VisibleElements()[1].name + "-icon").text);
        }
    }
}
