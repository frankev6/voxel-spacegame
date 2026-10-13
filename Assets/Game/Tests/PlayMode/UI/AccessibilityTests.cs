using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Game.App.Settings;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Persistence.Container;
using Game.UI.Data;
using Game.UI.Services;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI
{
    /// <summary>UI-013: production root/theme/toast services on the accepted runtime panel fixture.</summary>
    public sealed class AccessibilityTests
    {
        UITestPanel panel;
        UIConfig config;
        UIRoot ui;
        Clock clock;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            ui?.Dispose(); ui = null; panel?.Dispose(); panel = null;
            if (config != null) Object.Destroy(config); config = null;
            yield return null;
        }
        void Build()
        {
            panel = new UITestPanel(1280, 720);
            config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
            config.PanelSettings = panel.Settings; clock = new Clock();
            ui = new UIRoot(panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), clock);
        }
        static AccessibilityOptions Options(TextScale text = TextScale.Percent100, float scale = 1, bool motion = false)
            => new AccessibilityOptions { UiScale = scale, TextScale = text, ReduceMotion = motion };
        void Step(float dt) { clock.FrameCount++; clock.UnscaledTime += dt; ui.Tick(dt); }

        [UnityTest] public IEnumerator TextScale130_FontSizeScaled()
        {
            Build();
            var label = new Label("Readable text") { name = "accessibility-body" }; label.AddToClassList(UIStyleClasses.TextM);
            ui.GetLayer(UILayer.Screens).Add(label);
            float baseline = TextToken("--text-m");
            yield return Until(() => Mathf.Abs(label.resolvedStyle.fontSize - baseline) < .01f, "baseline font");
            ui.ApplyAccessibility(Options(TextScale.Percent130));
            float scaled = Mathf.Round(baseline * 1.30f);
            yield return Until(() => Mathf.Abs(label.resolvedStyle.fontSize - scaled) < .01f, "130% font");
            Assert.IsTrue(ui.RootElement.ClassListContains(UIStyleClasses.TextScale130));
            Assert.IsFalse(ui.RootElement.ClassListContains(UIStyleClasses.TextScale115));
            ui.ApplyAccessibility(Options(TextScale.Percent115));
            yield return Until(() => Mathf.Abs(label.resolvedStyle.fontSize - Mathf.Round(baseline * 1.15f)) < .01f, "115% font");
            Assert.IsFalse(ui.RootElement.ClassListContains(UIStyleClasses.TextScale130));
            Assert.IsTrue(ui.RootElement.ClassListContains(UIStyleClasses.TextScale115));
            ui.ApplyAccessibility(Options());
            yield return Until(() => Mathf.Abs(label.resolvedStyle.fontSize - baseline) < .01f, "restored font");
            Assert.IsFalse(ui.RootElement.ClassListContains(UIStyleClasses.TextScale130)); Assert.IsFalse(ui.RootElement.ClassListContains(UIStyleClasses.TextScale115));
        }

        [UnityTest] public IEnumerator UiScale_Clamped()
        {
            Build();
            ui.ApplyAccessibility(Options(scale: 2)); Assert.AreEqual(config.MaxUiScale, panel.Settings.scale);
            ui.ApplyAccessibility(Options(scale: .1f)); Assert.AreEqual(config.MinUiScale, panel.Settings.scale);
            ui.ApplyAccessibility(Options(scale: 1.1f)); Assert.AreEqual(1.1f, panel.Settings.scale);
            ui.ApplyAccessibility(Options(scale: float.NaN)); Assert.AreEqual(1, panel.Settings.scale);
            // Real SettingsService -> deferred SettingsChanged -> SettingsApplier -> registered UIRoot.
            string folder = Path.Combine(Path.GetTempPath(), "UI013_" + Guid.NewGuid().ToString("N"));
            Game.Core.Services.TryGet<UIRoot>(out var previous);
            if (previous != null) Game.Core.Services.Unregister(previous);
            Game.Core.Services.Register(ui);
            try
            {
                using var settings = new SettingsService(new NewtonsoftSaveJsonCodec(), new SettingsPaths(Path.Combine(folder, "settings.json")));
                settings.Load(); using var applier = new SettingsApplier(settings);
                SettingsApplier.Apply(SettingsSection.Accessibility, settings.Current);
                Assert.AreEqual(1, panel.Settings.scale); Assert.IsFalse(ui.ReduceMotion);
                var edited = settings.CreateEditableCopy(); edited.Accessibility.UiScale = 2;
                edited.Accessibility.TextScaleIndex = 2; edited.Accessibility.ReduceMotion = true;
                settings.Apply(edited, SettingsSection.Accessibility);
                Assert.AreEqual(1, panel.Settings.scale, "UI changes follow deferred event delivery.");
                EventHub.DrainAll();
                Assert.AreEqual(1.5f, panel.Settings.scale); Assert.IsTrue(ui.ReduceMotion);
                Assert.IsTrue(ui.RootElement.ClassListContains(UIStyleClasses.TextScale130));
                Assert.IsTrue(ui.RootElement.ClassListContains(UIStyleClasses.ReduceMotion));
                edited = settings.CreateEditableCopy(); edited.Accessibility.UiScale = .9f;
                edited.Accessibility.TextScaleIndex = 1; edited.Accessibility.ReduceMotion = false;
                settings.Apply(edited, SettingsSection.Accessibility); EventHub.DrainAll();
                Assert.AreEqual(.9f, panel.Settings.scale); Assert.IsFalse(ui.ReduceMotion);
                Assert.IsTrue(ui.RootElement.ClassListContains(UIStyleClasses.TextScale115));
                Assert.IsFalse(ui.RootElement.ClassListContains(UIStyleClasses.TextScale130));
                settings.ResetToDefaults(SettingsSection.Accessibility); EventHub.DrainAll();
                Assert.AreEqual(1, panel.Settings.scale); Assert.IsFalse(ui.RootElement.ClassListContains(UIStyleClasses.TextScale115));
            }
            finally
            {
                Game.Core.Services.Unregister(ui); if (previous != null) Game.Core.Services.Register(previous);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ReduceMotion_ToastExitImmediate()
        {
            Build(); yield return null;
            ui.Notifications.Post(new Notification { Label = "Normal toast" });
            var toast = ui.GetLayer(UILayer.Toasts).Q<VisualElement>("toast-0"); Assert.IsNotNull(toast);
            yield return Until(() => toast.resolvedStyle.transitionDuration.Count > 0
                && toast.resolvedStyle.transitionDuration[0].value > 0, "normal toast duration");
            Step(config.ToastLifetimeSeconds);
            Assert.AreEqual(1, ui.Notifications.VisibleCount, "Normal motion keeps the toast during its exit transition.");
            ui.ApplyAccessibility(Options(motion: true)); Step(0);
            Assert.AreEqual(0, ui.Notifications.VisibleCount, "Turning on reduced motion ends an exit already in progress.");
            ui.Notifications.Post(new Notification { Label = "Reduced toast" });
            Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastEnter));
            var probes = new List<VisualElement> { toast };
            foreach (string className in new[] { UIStyleClasses.Btn, UIStyleClasses.Tooltip, UIStyleClasses.ModalWindow })
            {
                var probe = new VisualElement { name = "motion-" + probes.Count }; probe.AddToClassList(className);
                ui.GetLayer(UILayer.Screens).Add(probe); probes.Add(probe);
            }
            yield return Until(() => probes.TrueForAll(HasZeroDuration), "reduced transition durations");
            Assert.IsTrue(ui.ReduceMotion); Assert.IsTrue(ui.RootElement.ClassListContains(UIStyleClasses.ReduceMotion));
            Step(config.ToastLifetimeSeconds);
            Assert.AreEqual(0, ui.Notifications.VisibleCount); Assert.AreEqual(0, ui.Notifications.QueuedCount);
            Assert.IsTrue(toast.ClassListContains(UIStyleClasses.IsHidden)); Assert.IsFalse(toast.ClassListContains(UIStyleClasses.ToastExit));
            ui.ApplyAccessibility(Options());
            Assert.IsFalse(ui.ReduceMotion); Assert.IsFalse(ui.RootElement.ClassListContains(UIStyleClasses.ReduceMotion));
            ui.Notifications.Post(new Notification { Label = "Motion restored" }); Step(config.ToastLifetimeSeconds);
            Assert.AreEqual(1, ui.Notifications.VisibleCount, "Disabling the option restores the normal exit delay.");
        }

        [UnityTest] public IEnumerator Gallery_TextScale130_NoOverflow()
        {
            Build(); ui.ApplyAccessibility(Options(TextScale.Percent130));
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Game/Tests/PlayMode/UI/Fixtures/ControlsGallery.uxml");
            var gallery = asset.CloneTree(); gallery.AddToClassList(UIStyleClasses.Grow); ui.GetLayer(UILayer.Screens).Add(gallery);
            float titleSize = Mathf.Round(TextToken("--text-l") * 1.30f);
            var title = gallery.Q<Label>("gallery-buttons-title"); Assert.IsNotNull(title);
            yield return Until(() => title.resolvedStyle.fontSize == titleSize
                && title.contentRect.width > 0, "scaled gallery layout");
            var issues = new List<LayoutIssue>(); UILayoutAudit.Run(gallery, panel.Viewport, issues);
            foreach (var issue in issues) Assert.AreNotEqual(LayoutIssueKind.TextOverflow, issue.Kind, issue.ElementPath);
            // The accepted audit excludes ScrollView content. Check the actual named gallery text too,
            // so this acceptance cannot pass merely because every control lives in that skipped subtree.
            int checkedText = 0;
            foreach (var text in gallery.Query<TextElement>().ToList())
            {
                if (string.IsNullOrEmpty(text.name) || string.IsNullOrEmpty(text.text) || text.resolvedStyle.display == DisplayStyle.None) continue;
                Assert.Greater(text.contentRect.width, 0, text.name + " must have resolved content geometry.");
                bool nowrap = text.resolvedStyle.whiteSpace == WhiteSpace.NoWrap;
                if (nowrap && text.ClassListContains(UIStyleClasses.TextEllipsis)) continue;
                var size = text.MeasureTextSize(text.text, nowrap ? 0 : text.contentRect.width,
                    nowrap ? VisualElement.MeasureMode.Undefined : VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined);
                if (nowrap) Assert.LessOrEqual(size.x, text.contentRect.width + .5f, text.name);
                else Assert.LessOrEqual(size.y, text.contentRect.height + .5f, text.name);
                checkedText++;
            }
            Assert.Greater(checkedText, 15, "Named controls in the scroll content must be checked.");
        }

        static bool HasZeroDuration(VisualElement element)
        {
            var durations = element.resolvedStyle.transitionDuration;
            if (durations.Count == 0) return false;
            for (int i = 0; i < durations.Count; i++) if (durations[i].value != 0) return false;
            return true;
        }
        static float TextToken(string name)
        {
            string css = File.ReadAllText(Path.Combine(Application.dataPath, "Game", "UI", "Theme", "tokens.uss"));
            return float.Parse(Regex.Match(css, Regex.Escape(name) + @"\s*:\s*([0-9]+)px").Groups[1].Value, CultureInfo.InvariantCulture);
        }
        static IEnumerator Until(Func<bool> ready, string description)
        {
            for (int frame = 0; frame < 8 && !ready(); frame++) yield return null;
            Assert.IsTrue(ready(), description + " did not settle within eight panel frames.");
        }
        sealed class Clock : IFrameClock { public int FrameCount { get; set; } public float UnscaledTime { get; set; } }
    }
}
