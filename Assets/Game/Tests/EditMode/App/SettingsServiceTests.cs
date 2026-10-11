using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Game.App.Settings;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Persistence.Container;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.App
{
    /// <summary>FND-004: SettingsService (SPEC-01 §5.6) on a temporary directory.</summary>
    public sealed class SettingsServiceTests
    {
        string dir;
        SettingsPaths paths;
        readonly List<SettingsChanged> events = new List<SettingsChanged>();
        void OnChanged(SettingsChanged e) => events.Add(e);

        [SetUp]
        public void SetUp()
        {
            dir = Path.Combine(Path.GetTempPath(), "SettingsServiceTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            paths = new SettingsPaths(Path.Combine(dir, "settings.json"));
            EventHub.Reset(); events.Clear(); EventBus<SettingsChanged>.Subscribe(OnChanged);
        }

        [TearDown] public void TearDown() { EventHub.Reset(); Directory.Delete(dir, true); }

        SettingsService NewService() { var s = new SettingsService(new NewtonsoftSaveJsonCodec(), paths); s.Load(); return s; }

        [Test]
        public void RoundTrip()
        {
            var a = NewService();
            var e = a.CreateEditableCopy();
            e.Audio.MasterVolume = 0.1f; e.Audio.MusicVolume = 0.2f; e.Audio.SfxVolume = 0.3f; e.Audio.UiVolume = 0.4f; e.Audio.AmbienceVolume = 0.5f; e.Audio.MuteWhenUnfocused = false;
            e.Video.ResolutionWidth = 1920; e.Video.ResolutionHeight = 1080; e.Video.RefreshRateHz = 144; e.Video.FullScreenMode = FullScreenMode.Windowed;
            e.Video.VSync = false; e.Video.TargetFrameRate = 120; e.Video.QualityLevel = 2; e.Video.VfxQuality = 1;
            e.Controls.MouseSensitivity = 2.5f; e.Controls.InvertCameraY = true; e.Controls.CameraZoomSensitivity = 0.5f; e.Controls.BindingOverridesJson = "{\"bindings\":[]}";
            e.Gameplay.AutosaveIntervalMinutes = 15; e.Gameplay.ShowControlHints = false;
            e.Accessibility.UiScale = 1.25f; e.Accessibility.TextScaleIndex = 2; e.Accessibility.ReduceMotion = true; e.Accessibility.ScreenShakeScale = 0.25f;
            a.Apply(e, SettingsSection.All);
            a.Flush();
            Assert.IsTrue(File.Exists(paths.FilePath)); Assert.IsFalse(File.Exists(paths.TempPath));
            var b = NewService();
            Assert.IsTrue(b.Current.Audio.ValueEquals(e.Audio)); Assert.IsTrue(b.Current.Video.ValueEquals(e.Video));
            Assert.IsTrue(b.Current.Controls.ValueEquals(e.Controls)); Assert.IsTrue(b.Current.Gameplay.ValueEquals(e.Gameplay));
            Assert.IsTrue(b.Current.Accessibility.ValueEquals(e.Accessibility));
            Assert.AreEqual(SettingsSection.None, b.Current.DiffersFrom(a.Current));
        }

        [Test]
        public void Corrupt_FallsBack()
        {
            File.WriteAllText(paths.FilePath, "{ \"version\": 1, \"audio\": { \"MasterVolume\": ");
            LogAssert.Expect(LogType.Warning, new Regex("settings.json was unreadable"));
            var s = NewService();
            Assert.AreEqual(SettingsSection.None, s.Current.DiffersFrom(new GameSettings()), "defaults");
            Assert.IsFalse(File.Exists(paths.FilePath)); Assert.IsTrue(File.Exists(paths.BrokenPath));
            File.WriteAllText(paths.FilePath, "{\"version\":1,\"unknownSection\":{}}");
            LogAssert.Expect(LogType.Warning, new Regex("settings.json was unreadable"));
            NewService();
            Assert.IsTrue(File.Exists(paths.BrokenPath), "an older broken file is replaced");
        }

        [Test]
        public void NoChange_NoEvent()
        {
            var s = NewService();
            s.Apply(s.CreateEditableCopy(), SettingsSection.All);
            var e = s.CreateEditableCopy(); e.Audio.MasterVolume = 0.123f;
            s.Apply(e, SettingsSection.Video);   // the change is outside the applied sections
            EventHub.DrainAll();
            Assert.AreEqual(0, events.Count);
            s.Tick(10f); s.Flush();
            Assert.AreEqual(0, s.WriteCount, "nothing scheduled");
            Assert.IsFalse(File.Exists(paths.FilePath));
            e.Video.VSync = false;
            s.Apply(e, SettingsSection.Video);
            EventHub.DrainAll();
            Assert.AreEqual(1, events.Count); Assert.AreEqual(SettingsSection.Video, events[0].Sections);
        }

        [Test]
        public void Debounce_SingleWrite()
        {
            var s = NewService();
            for (int i = 0; i < 10; i++)
            {
                var e = s.CreateEditableCopy(); e.Audio.MasterVolume = 0.1f + i * 0.05f;
                s.Apply(e, SettingsSection.Audio);
                s.Tick(0.04f);                     // 10 changes within 0.4 s
            }
            Assert.AreEqual(0, s.WriteCount, "still inside the debounce");
            s.Tick(0.3f);
            Assert.AreEqual(0, s.WriteCount);
            s.Tick(0.3f);
            Assert.AreEqual(1, s.WriteCount, "one write after the last change settles");
            s.Tick(5f);
            Assert.AreEqual(1, s.WriteCount);
            Assert.AreEqual(0.55f, NewService().Current.Audio.MasterVolume, 1e-6f);
        }
    }
}
