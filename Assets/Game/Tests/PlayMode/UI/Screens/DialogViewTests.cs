using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Events;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>Exactly HUD-013's five cases on the real authored view, root/navigation/input and theme; no story runner.</summary>
    public sealed class DialogViewTests : InputTestFixture
    {
        [SetUp] public override void Setup()
        { base.Setup(); EventHub.Reset(); Services.Reset(); InputSystem.AddDevice<Keyboard>(); }
        [TearDown] public override void TearDown()
        { try { EventHub.Reset(); Services.Reset(); } finally { base.TearDown(); } }
        static void Populate(DialogViewFixture fixture, int options = 9)
        {
            fixture.Model.SpeakerName = "UI fixture";
            fixture.Model.LineText = string.Concat(Enumerable.Repeat("Test text ", 150));
            fixture.Model.LineVersion++;
            for (int i = 0; i < options; i++) fixture.Model.Options.Add(new DialogOptionViewModel
                { Number = i + 1, Text = "Option " + (i + 1), Enabled = true });
            fixture.Model.NotifyOptions();
        }
        static void Audit(DialogViewFixture fixture)
        {
            var issues = new List<LayoutIssue>();
            UILayoutAudit.Run(fixture.View.Root, fixture.Panel.Viewport, issues);
            Assert.IsEmpty(issues, string.Join("\n", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
        }
        [UnityTest] public IEnumerator NineOptionsLongLine_1280x720_NoIssues()
        {
            using var fixture = new DialogViewFixture(1280, 720); Populate(fixture); fixture.Show();
            yield return null; yield return null; // Native panel and dynamic option row measurement.
            Assert.AreEqual(1500, fixture.View.Root.Q<Label>("dialog-text-label").text.Length);
            var panel = fixture.View.Root.Q<VisualElement>("dialog-panel");
            Assert.AreEqual(640, panel.worldBound.width, 1); Assert.Less(panel.worldBound.xMin, 1280 * .5f);
            var text = fixture.View.Root.Q<ScrollView>("dialog-text"); Assert.Greater(text.verticalScroller.highValue, 0);
            var options = fixture.View.Root.Q<ListView>("dialog-options"); Assert.AreEqual(9, options.itemsSource.Count);
            Assert.Greater(options.Q<ScrollView>().verticalScroller.highValue, 0);
            options.ScrollToItem(8); yield return null;
            var last = fixture.View.Root.Q<Button>("dialog-option-9"); Assert.IsNotNull(last); Assert.IsTrue(last.enabledInHierarchy);
            var viewport = options.Q<ScrollView>().contentViewport.worldBound;
            Assert.GreaterOrEqual(last.worldBound.yMin, viewport.yMin - 1);
            Assert.LessOrEqual(last.worldBound.yMax, viewport.yMax + 1);
            DialogTestContext.Activate(last); Assert.AreEqual(9, fixture.Actions.Number); Assert.AreEqual(1, fixture.Actions.HostChoices);
            Audit(fixture);
        }
        [UnityTest] public IEnumerator NewLine_ScrollResetToTop()
        {
            using var fixture = new DialogViewFixture(); Populate(fixture, 1); fixture.Show();
            yield return null; yield return null;
            var text = fixture.View.Root.Q<ScrollView>("dialog-text");
            text.scrollOffset = new Vector2(0, text.verticalScroller.highValue); Assert.Greater(text.scrollOffset.y, 0);
            fixture.Model.LineVersion++; // Same text is a new delivery; no collection-change shortcut.
            Assert.AreEqual(Vector2.zero, text.scrollOffset); yield return null; Assert.AreEqual(Vector2.zero, text.scrollOffset);
            // SPEC-41's multi-line page remains compatible with the new LineVersion reset as well.
            for (int i = 0; i < 32; i++) fixture.Model.Lines.Add(new DialogLineViewModel { Text = fixture.Model.LineText });
            fixture.Model.NotifyLinesChanged(); yield return null; yield return null;
            var lines = fixture.View.Root.Q<ListView>("dialog-lines").Q<ScrollView>();
            lines.scrollOffset = new Vector2(0, lines.verticalScroller.highValue); Assert.Greater(lines.scrollOffset.y, 0);
            fixture.Model.LineVersion++; Assert.AreEqual(Vector2.zero, lines.scrollOffset);
            yield return null; Assert.AreEqual(Vector2.zero, lines.scrollOffset);
        }
        [UnityTest] public IEnumerator HudHiddenWhileOpen()
        {
            using var fixture = new DialogViewFixture(); Populate(fixture, 1);
            var hud = fixture.UI.GetLayer(UILayer.Hud); Assert.IsFalse(hud.ClassListContains(UIStyleClasses.IsHidden));
            fixture.Show(); yield return null;
            Assert.IsTrue(hud.ClassListContains(UIStyleClasses.IsHidden)); Assert.IsTrue(fixture.Pause.IsPaused);
            Assert.IsTrue(fixture.UI.Navigator.WorldPointerBlocked); Assert.AreEqual(InputContext.Dialog, fixture.Input.CurrentContext);
            fixture.UI.Confirm.Show(DialogTestContext.Confirm(), null); Assert.IsTrue(hud.ClassListContains(UIStyleClasses.IsHidden));
            fixture.UI.HandleBackPressed(); Assert.IsTrue(fixture.View.IsOpen); Assert.IsTrue(hud.ClassListContains(UIStyleClasses.IsHidden));
            fixture.UI.Navigator.Pop(fixture.View);
            Assert.IsFalse(hud.ClassListContains(UIStyleClasses.IsHidden)); Assert.IsFalse(fixture.Pause.IsPaused);
            Assert.IsFalse(fixture.UI.Navigator.WorldPointerBlocked); Assert.AreEqual(InputContext.Flight, fixture.Input.CurrentContext);
        }
        [UnityTest] public IEnumerator Portrait_DoesNotOverlapText()
        {
            using var fixture = new DialogViewFixture(); Populate(fixture); fixture.AddImages(); fixture.Show();
            yield return null; yield return null;
            var portrait = fixture.View.Root.Q<Image>("dialog-portrait"); var text = fixture.View.Root.Q<ScrollView>("dialog-text");
            Assert.AreEqual(96, portrait.worldBound.width, 1); Assert.AreEqual(96, portrait.worldBound.height, 1);
            Assert.Greater(text.worldBound.width, 0); Assert.LessOrEqual(portrait.worldBound.xMax, text.worldBound.xMin);
            Assert.IsFalse(portrait.worldBound.Overlaps(text.worldBound)); Audit(fixture);
            float width = text.worldBound.width; fixture.Model.HasPortrait = false; yield return null;
            Assert.AreEqual(DisplayStyle.None, fixture.View.Root.Q<VisualElement>("dialog-portrait-column").resolvedStyle.display);
            Assert.Greater(text.worldBound.width, width, "Removing the portrait returns its column to the text.");
        }
        [UnityTest] public IEnumerator OptionClick_CallsChoose()
        {
            using var fixture = new DialogViewFixture(); Populate(fixture); fixture.Show();
            yield return null; yield return null;
            var options = fixture.View.Root.Q<ListView>("dialog-options");
            var identities = new HashSet<VisualElement>();
            for (int i = 0; i < 9; i++)
            {
                options.ScrollToItem(i); yield return null;
                var button = fixture.View.Root.Q<Button>("dialog-option-" + (i + 1)); Assert.IsNotNull(button);
                foreach (var rowButton in options.Query<Button>().ToList())
                    if (rowButton.name.StartsWith("dialog-option-", System.StringComparison.Ordinal)) identities.Add(rowButton);
                DialogTestContext.Activate(button);
                Assert.AreEqual(i + 1, fixture.Actions.Number); Assert.AreEqual(i + 1, fixture.Actions.HostChoices);
            }
            Assert.AreEqual(0, fixture.Actions.Clicks, "Numbered host buttons call Choose(number).");
            // Another complete menu must reuse the existing nine-row pool while its data identities change.
            fixture.Model.Options.Clear();
            for (int i = 0; i < 9; i++) fixture.Model.Options.Add(new DialogOptionViewModel { Number = i + 1, Text = "Second option " + (i + 1), Enabled = true });
            fixture.Model.NotifyOptions();
            for (int i = 0; i < 9; i++)
            {
                options.ScrollToItem(i); yield return null;
                foreach (var rowButton in options.Query<Button>().ToList())
                    if (rowButton.name.StartsWith("dialog-option-", System.StringComparison.Ordinal)) identities.Add(rowButton);
            }
            Assert.LessOrEqual(identities.Count, 9, "Scrolling and replacing a standard menu must reuse the nine prebuilt choices.");
            var ninth = fixture.View.Root.Q<Button>("dialog-option-9");
            fixture.Model.Options[8].Enabled = false; fixture.Model.Options[8].DisabledReason = "Unavailable";
            Assert.IsFalse(ninth.enabledSelf); Assert.AreEqual("Unavailable", ninth.tooltip);
            DialogTestContext.Activate(ninth); Assert.AreEqual(9, fixture.Actions.HostChoices);
            var old = fixture.Model.Options[0]; fixture.Model.Options.Clear();
            fixture.Model.Options.Add(new DialogOptionViewModel { Number = 7, Text = "Replacement option", Enabled = true });
            fixture.Model.NotifyOptions(); options.ScrollToItem(0); yield return null;
            var replacement = fixture.View.Root.Q<Button>("dialog-option-1"); Assert.IsNotNull(replacement);
            old.Text = "Stale option";
            Assert.AreEqual("Replacement option", replacement.Q<Label>("dialog-option-text").text);
            DialogTestContext.Activate(replacement); Assert.AreEqual(7, fixture.Actions.Number); Assert.AreEqual(10, fixture.Actions.HostChoices);
            fixture.Model.Options.Clear(); fixture.Model.NotifyOptions(); fixture.Model.CanContinue = true;
            var continueButton = fixture.View.Root.Q<Button>("dialog-continue"); Assert.IsTrue(continueButton.enabledSelf);
            DialogTestContext.Activate(continueButton); Assert.AreEqual(1, fixture.Actions.Continues);
            fixture.UI.Navigator.Pop(fixture.View); old.Text = "Closed option";
            DialogTestContext.Activate(replacement); Assert.AreEqual(10, fixture.Actions.HostChoices);
            fixture.Show(); yield return null;
            DialogTestContext.Activate(continueButton); Assert.AreEqual(2, fixture.Actions.Continues, "Reopening keeps one callback.");
        }
    }
}
