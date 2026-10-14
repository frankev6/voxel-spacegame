using System;
using System.Globalization;
using System.Reflection;
using Game.Core.Events;
using Game.Narrative.Dialog;
using Game.Tests.EditMode.Data;
using Game.Tests.EditMode.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.DevTools
{
    public sealed class DialogHarnessCommandsTests
    {
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() { EventHub.Reset(); TestContentFactory.DestroyAll(); }
        static Type Harness => Type.GetType("Game.DevTools.Editor.Narrative.DialogHarnessCommands, Game.DevTools.Editor", true);
        static string Snapshot(DialogPresenterFixture fixture = null) => (string)Harness.GetMethod("Snapshot", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { fixture?.Service, fixture?.Presenter, null, null });
        static string Choose(DialogPresenterFixture fixture, int number) => (string)Harness.GetMethod("Choose", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { fixture.Service, fixture.Presenter, number });
        [Serializable] sealed class Page
        {
            public bool available = false, active = false, waitingForChoice = false, canContinue = false;
            public int beatIndex = 0; public string beat = null, node = null, reason = null;
            public Line[] lines = null; public Option[] options = null;
        }
        [Serializable] sealed class Line { public string text = null, speaker = null; public bool draft = false; }
        [Serializable] sealed class Option
        { public int number = 0, id = 0, synthetic = 0; public string numberLabel = null, text = null; public bool available = false, quest = false; }

        [Test] public void Snapshot_FormatsOptions()
        {
            var unavailable = JsonUtility.FromJson<Page>(Snapshot()); Assert.IsFalse(unavailable.available);
            StringAssert.Contains("production runner not installed", unavailable.reason); Assert.AreEqual(0, unavailable.lines.Length);
            using var f = new DialogPresenterFixture();
            var choices = new[] {
                new DialogOption { Id = 11, Text = "Hidden", HideWhenUnavailable = true },
                new DialogOption { Id = 42, Text = "Locked, \"door\"" },
                new DialogOption { Id = 77, Text = "Go\n雪\\path", Available = true, IsQuest = true } };
            f.Start(host =>
            {
                host.OnLine(new DialogLine("test_00", "Hello \"captain\"\n#7\\雪", "NORA", "", "", "", "start", true));
                host.OnOptions(choices, choices.Length);
            });
            string json; var culture = CultureInfo.CurrentCulture;
            try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR"); json = Snapshot(f); }
            finally { CultureInfo.CurrentCulture = culture; }
            var page = JsonUtility.FromJson<Page>(json);
            Assert.IsTrue(page.available); Assert.IsTrue(page.active); Assert.IsTrue(page.waitingForChoice); Assert.IsFalse(page.canContinue);
            Assert.AreEqual(0, page.beatIndex); Assert.AreEqual("", page.beat); Assert.AreEqual("start", page.node);
            Assert.AreEqual(1, page.lines.Length); Assert.AreEqual("Hello \"captain\"\n#7\\雪", page.lines[0].text);
            Assert.AreEqual("NORA", page.lines[0].speaker); Assert.IsTrue(page.lines[0].draft);
            Assert.AreEqual(2, page.options.Length); Assert.AreEqual(1, page.options[0].number); Assert.AreEqual("1", page.options[0].numberLabel);
            Assert.AreEqual(42, page.options[0].id); Assert.IsFalse(page.options[0].available); Assert.AreEqual(choices[1].Text, page.options[0].text);
            Assert.AreEqual(2, page.options[1].number); Assert.AreEqual(77, page.options[1].id); Assert.IsTrue(page.options[1].available);
            Assert.IsTrue(page.options[1].quest); Assert.AreEqual(choices[2].Text, page.options[1].text); StringAssert.DoesNotContain("Hidden", json);
            f.Service.OnNodeStarted("detour"); Assert.AreEqual("detour", JsonUtility.FromJson<Page>(Snapshot(f)).node);
            f.Service.OnNodeCompleted("detour"); Assert.AreEqual("start", JsonUtility.FromJson<Page>(Snapshot(f)).node);
            StringAssert.StartsWith("failed:", Choose(f, 0)); StringAssert.StartsWith("failed:", Choose(f, 10));
            StringAssert.Contains("unavailable", Choose(f, 1)); StringAssert.Contains("not found", Choose(f, 3));
            Assert.AreEqual("accepted: 2", Choose(f, 2)); Assert.AreEqual(0, f.Script.Selections, "Selection stays queued for the canonical presenter tick.");
            StringAssert.Contains("not waiting", Choose(f, 2)); Assert.IsFalse(JsonUtility.FromJson<Page>(Snapshot(f)).waitingForChoice);
            f.Presenter.Tick(); Assert.AreEqual(77, f.Script.Selected); Assert.AreEqual(1, f.Script.Selections);
            var inactive = JsonUtility.FromJson<Page>(Snapshot(f)); Assert.IsFalse(inactive.active); Assert.AreEqual("", inactive.node);
            Assert.AreEqual(0, inactive.lines.Length); Assert.AreEqual(0, inactive.options.Length); StringAssert.Contains("no active", Choose(f, 1));
            f.Start(host => host.OnLine(DialogPresenterFixture.Line("last page"))); f.Presenter.Tick();
            var end = JsonUtility.FromJson<Page>(Snapshot(f)); Assert.IsTrue(end.canContinue); Assert.AreEqual("start", end.node);
            Assert.AreEqual(2, end.options[0].synthetic); Assert.AreEqual("accepted: 1", Choose(f, 1)); Assert.IsFalse(f.Service.IsActive);
            f.Start(host => host.OnOptions(new[] { new DialogOption { Text = "Unavailable" } }, 1));
            var fallback = JsonUtility.FromJson<Page>(Snapshot(f)); Assert.AreEqual(1, fallback.options[1].synthetic);
            Assert.AreEqual("accepted: 2", Choose(f, 2)); Assert.IsFalse(f.Service.IsActive);
        }
    }
}
