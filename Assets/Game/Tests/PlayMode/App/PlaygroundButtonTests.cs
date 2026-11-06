using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Flow;
using Game.UI.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.App
{
    /// <summary>Owner (2026-11-05): "why can't I jump into the playground from the button ... I have to go to sandbox and then
    /// playground". The main menu's Enter playground button must reach the playground in one click.</summary>
    public sealed class PlaygroundButtonTests : SessionCommandFixture
    {
        [UnityTest] public IEnumerator MainMenuButton_EntersPlayground() => ClickEnters();
        internal IEnumerator ClickEnters()
        {
            var warnings = new List<string>();
            void Capture(string message, string stack, LogType type) { if (type != LogType.Log) warnings.Add(type + ": " + message); }
            Application.logMessageReceived += Capture;
            try
            {
                yield return null; yield return null;   // the launcher's display is set in the playground's Tick
                var ui = Services.Get<UIRoot>();
                var button = ui.RootElement.Q<Button>("btn-enter-playground");
                Assert.NotNull(button, "The launcher exists on the main menu");
                Assert.AreNotEqual(DisplayStyle.None, button.resolvedStyle.display, "The button is shown on the main menu");
                for (var e = button.parent; e != null; e = e.parent)
                    Assert.AreNotEqual(DisplayStyle.None, e.resolvedStyle.display, "Hidden by its parent " + e.name);
                var picked = button.panel.Pick(button.worldBound.center);
                Assert.IsTrue(picked == button || button.Contains(picked),
                    "A click at the button reaches it, not " + (picked == null ? "nothing" : picked.name + " (" + picked.GetType().Name + ")")
                    + "; button bounds " + button.worldBound);

                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = button; button.SendEvent(submit); }
                float deadline = Time.realtimeSinceStartup + 60;
                while (Time.realtimeSinceStartup < deadline && !Host.Commands.Execute("playground status", Host.Context).Text.Contains("Playground active"))
                    yield return null;
                Assert.IsTrue(Host.Commands.Execute("playground status", Host.Context).Text.Contains("Playground active"),
                    "One click enters the playground. Status: " + Host.Commands.Execute("playground status", Host.Context).Text
                    + " Warnings: " + string.Join(" | ", warnings));
                Assert.IsTrue(Game.Session.IsSandbox);
            }
            finally { Application.logMessageReceived -= Capture; }
        }
    }

    /// <summary>The owner's case: Play pressed in an empty scene. Startup leaves a non-game scene alone, so the screen is black apart from
    /// the dev buttons, and the button refused ("Leave the current editor…"). It now loads the playground from there too.</summary>
    public sealed class PlaygroundButtonNonGameSceneTests : SessionCommandFixture
    {
        protected override bool StartAtMainMenu => false;
        [UnityTest] public IEnumerator NonGameScene_ButtonEntersPlayground()
        {
            Assert.AreEqual(GameScene.None, Game.Flow.CurrentScene, "Booted in the Test Runner's scene, not one of the game's");
            yield return new PlaygroundButtonTests().ClickEnters();
        }
    }
}
