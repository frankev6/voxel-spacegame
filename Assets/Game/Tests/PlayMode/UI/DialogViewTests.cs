using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI
{
    public sealed class DialogViewTests : InputTestFixture
    {
        Keyboard keyboard;
        [SetUp] public override void Setup()
        { base.Setup(); EventHub.Reset(); Services.Reset(); keyboard = InputSystem.AddDevice<Keyboard>(); }
        [TearDown] public override void TearDown()
        { try { EventHub.Reset(); Services.Reset(); } finally { base.TearDown(); } }
        void Tap(ButtonControl key) { Press(key); InputSystem.Update(); Release(key); InputSystem.Update(); }
        [Test] public void Push_SetsContextAndPause()
        {
            using var f = new DialogViewFixture(); f.Populate();
            Assert.AreEqual(InputContext.Flight, f.Input.CurrentContext); Assert.IsFalse(f.Pause.IsPaused);
            f.Show(); Assert.AreEqual(InputContext.Dialog, f.Input.CurrentContext); Assert.IsTrue(f.Pause.IsPaused);
            Assert.IsTrue(f.Input.Controls.Dialog.enabled); Assert.IsFalse(f.Input.Controls.Flight.enabled);
            Assert.IsTrue(f.UI.GetLayer(UILayer.Hud).ClassListContains(UIStyleClasses.IsHidden));
            Assert.IsTrue(f.UI.Navigator.WorldPointerBlocked);
            f.UI.Navigator.Pop(f.View); Assert.AreEqual(InputContext.Flight, f.Input.CurrentContext);
            Assert.IsFalse(f.Pause.IsPaused); Assert.IsFalse(f.UI.GetLayer(UILayer.Hud).ClassListContains(UIStyleClasses.IsHidden));
        }
        [Test] public void NumberKeys_Choose()
        {
            using var f = new DialogViewFixture(); f.Populate(); f.Show();
            var keys = new[] { keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key,
                keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key, keyboard.digit9Key };
            for (int i = 0; i < keys.Length; i++) { Tap(keys[i]); Assert.AreEqual(i + 1, f.Actions.Number); }
            Assert.AreEqual(9, f.Actions.Choices); Tap(keyboard.spaceKey); Tap(keyboard.enterKey); Assert.AreEqual(2, f.Actions.Continues);
            Tap(keyboard.escapeKey); Assert.AreEqual(1, f.Actions.Backs); Assert.IsTrue(f.View.IsOpen);
            var list = f.View.Root.Q<ListView>("dialog-options"); var row = list.makeItem(); f.View.Root.Add(row);
            f.Model.Options[0].IsAvailable = false; f.Model.Options[0].IsQuest = true;
            list.bindItem(row, 0); Assert.IsFalse(row.Q<Button>().enabledSelf);
            Assert.IsTrue(row.Q<Button>().ClassListContains("dialog__option--quest"));
            list.unbindItem(row, 0); Assert.IsNull(row.dataSource); Assert.IsFalse(row.Q<Button>().ClassListContains("dialog__option--quest"));
            f.Model.Options[1].Synthetic = 1; list.bindItem(row, 1);
            Assert.AreEqual("End conversation", row.Q<Label>("dialog-option-text").text);
            DialogTestContext.Activate(row.Q<Button>()); Assert.AreEqual(1, f.Actions.Index); Assert.AreEqual(1, f.Actions.Clicks);
            f.Model.Options[2].Synthetic = 2; list.unbindItem(row, 1); list.bindItem(row, 2);
            Assert.AreEqual("Continue", row.Q<Label>("dialog-option-text").text);
            DialogTestContext.Activate(row.Q<Button>()); Assert.AreEqual(2, f.Actions.Index); Assert.AreEqual(2, f.Actions.Clicks);
            list.unbindItem(row, 2); list.destroyItem(row); row.RemoveFromHierarchy();
            f.UI.Navigator.Pop(f.View); Tap(keyboard.digit1Key); Assert.AreEqual(9, f.Actions.Choices);
            f.Show(); Tap(keyboard.digit1Key); Assert.AreEqual(10, f.Actions.Choices, "Reopening attaches each action exactly once.");
        }
        [UnityTest] public IEnumerator NewPage_ScrollsToTop()
        {
            using var f = new DialogViewFixture(); f.Populate(32); f.Show();
            yield return null; yield return null; // Bounded panel layout and dynamic-height measurement, no timed wait.
            var list = f.View.Root.Q<ListView>("dialog-lines"); var scroll = list.Q<ScrollView>();
            scroll.scrollOffset = new Vector2(0, scroll.verticalScroller.highValue);
            Assert.Greater(scroll.scrollOffset.y, 0, "The old page must genuinely be scrolled.");
            f.Model.Lines.Clear(); f.Model.Lines.Add(new DialogLineViewModel { Text = "New page" });
            f.Model.ScrollToLine = 0; f.Model.NotifyLinesChanged();
            Assert.AreEqual(0, scroll.scrollOffset.y, "Collection changes reset the page even when ScrollToLine was already zero.");
            yield return null;
            Assert.AreEqual(0, scroll.scrollOffset.y);
            var row = list.makeItem(); f.Model.Lines[0].SpeakerStyleClass = "speaker--nora"; f.Model.Lines[0].IsDraft = true;
            list.bindItem(row, 0); Assert.IsTrue(row.Q<Label>("dialog-line-speaker").ClassListContains("speaker--nora"));
            list.unbindItem(row, 0); Assert.IsNull(row.dataSource);
            Assert.IsFalse(row.Q<Label>("dialog-line-speaker").ClassListContains("speaker--nora"));
            Assert.IsFalse(row.ClassListContains("dialog__line--draft")); Assert.IsFalse(row.ClassListContains("dialog__line--enter"));
            f.UI.RootElement.AddToClassList(UIStyleClasses.ReduceMotion); list.bindItem(row, 0);
            Assert.IsFalse(row.ClassListContains("dialog__line--enter")); list.unbindItem(row, 0); list.destroyItem(row);
        }
        [UnityTest] public IEnumerator Layout_NoIssues()
        {
            var sizes = new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
            var issues = new List<LayoutIssue>();
            foreach (var size in sizes)
            {
                using var f = new DialogViewFixture(size.x, size.y); f.Populate(12, 12); f.AddImages(); f.Show();
                yield return null; yield return null;
                Assert.Greater(f.View.Root.Q<VisualElement>("dialog-panel").worldBound.width, 0);
                UILayoutAudit.Run(f.View.Root, f.Panel.Viewport, issues);
                Assert.AreEqual(0, issues.Count, string.Join("\n", issues));
            }
        }
    }
}
