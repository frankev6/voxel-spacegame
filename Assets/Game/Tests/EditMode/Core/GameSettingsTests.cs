using Game.Core.Settings;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Core
{
    /// <summary>CORE-011: settings model (SPEC-02 §4.9, §5.11).</summary>
    public sealed class GameSettingsTests
    {
        [Test]
        public void Defaults_MatchSpec()
        {
            var s = new GameSettings();
            Assert.AreEqual(1, GameSettings.CurrentVersion);
            Assert.AreEqual(0.8f, s.Audio.MasterVolume); Assert.AreEqual(0.6f, s.Audio.MusicVolume); Assert.AreEqual(0.8f, s.Audio.SfxVolume);
            Assert.AreEqual(0.7f, s.Audio.UiVolume); Assert.AreEqual(0.6f, s.Audio.AmbienceVolume); Assert.IsTrue(s.Audio.MuteWhenUnfocused);
            Assert.AreEqual(0, s.Video.ResolutionWidth); Assert.AreEqual(0, s.Video.ResolutionHeight); Assert.AreEqual(0, s.Video.RefreshRateHz);
            Assert.AreEqual(FullScreenMode.FullScreenWindow, s.Video.FullScreenMode); Assert.IsTrue(s.Video.VSync);
            Assert.AreEqual(-1, s.Video.TargetFrameRate); Assert.AreEqual(-1, s.Video.QualityLevel); Assert.AreEqual(2, s.Video.VfxQuality);
            Assert.AreEqual(1f, s.Controls.MouseSensitivity); Assert.IsFalse(s.Controls.InvertCameraY);
            Assert.AreEqual(1f, s.Controls.CameraZoomSensitivity); Assert.AreEqual("", s.Controls.BindingOverridesJson);
            Assert.AreEqual(5, s.Gameplay.AutosaveIntervalMinutes); Assert.IsTrue(s.Gameplay.ShowControlHints);
            Assert.AreEqual(1f, s.Accessibility.UiScale); Assert.AreEqual(0, s.Accessibility.TextScaleIndex);
            Assert.IsFalse(s.Accessibility.ReduceMotion); Assert.AreEqual(1f, s.Accessibility.ScreenShakeScale);
            var clamped = s.Clone(); clamped.Clamp();
            Assert.AreEqual(SettingsSection.None, clamped.DiffersFrom(s), "defaults are inside every range");
        }

        [Test]
        public void Clamp_EnforcesRanges()
        {
            var s = new GameSettings();
            s.Audio.MasterVolume = 2f; s.Audio.MusicVolume = -1f; s.Audio.SfxVolume = 1.5f; s.Audio.UiVolume = -0.1f; s.Audio.AmbienceVolume = 9f;
            s.Video.ResolutionWidth = 100; s.Video.ResolutionHeight = 99999; s.Video.RefreshRateHz = 1000;
            s.Video.TargetFrameRate = 5; s.Video.QualityLevel = 40; s.Video.VfxQuality = 7; s.Video.FullScreenMode = (FullScreenMode)42;
            s.Controls.MouseSensitivity = 0f; s.Controls.CameraZoomSensitivity = 50f; s.Controls.BindingOverridesJson = null;
            s.Accessibility.UiScale = 3f; s.Accessibility.TextScaleIndex = 9; s.Accessibility.ScreenShakeScale = -2f;
            s.Clamp();
            Assert.AreEqual(1f, s.Audio.MasterVolume); Assert.AreEqual(0f, s.Audio.MusicVolume); Assert.AreEqual(1f, s.Audio.SfxVolume);
            Assert.AreEqual(0f, s.Audio.UiVolume); Assert.AreEqual(1f, s.Audio.AmbienceVolume);
            Assert.AreEqual(640, s.Video.ResolutionWidth); Assert.AreEqual(4320, s.Video.ResolutionHeight); Assert.AreEqual(500, s.Video.RefreshRateHz);
            Assert.AreEqual(30, s.Video.TargetFrameRate); Assert.AreEqual(15, s.Video.QualityLevel); Assert.AreEqual(2, s.Video.VfxQuality);
            Assert.AreEqual(FullScreenMode.FullScreenWindow, s.Video.FullScreenMode);
            Assert.AreEqual(0.1f, s.Controls.MouseSensitivity); Assert.AreEqual(5f, s.Controls.CameraZoomSensitivity);
            Assert.AreEqual("", s.Controls.BindingOverridesJson, "null string → empty");
            Assert.AreEqual(1.5f, s.Accessibility.UiScale); Assert.AreEqual(2, s.Accessibility.TextScaleIndex); Assert.AreEqual(0f, s.Accessibility.ScreenShakeScale);

            var v = new VideoSettingsData { ResolutionWidth = 1920, ResolutionHeight = 0, QualityLevel = -7, TargetFrameRate = -1 };
            v.Clamp();
            Assert.AreEqual(0, v.ResolutionWidth, "one resolution field 0 → both 0"); Assert.AreEqual(0, v.ResolutionHeight);
            Assert.AreEqual(-1, v.QualityLevel); Assert.AreEqual(-1, v.TargetFrameRate);
        }

        [Test]
        public void Clamp_NaN_ToDefault()
        {
            var s = new GameSettings();
            s.Audio.MasterVolume = float.NaN; s.Audio.MusicVolume = float.PositiveInfinity; s.Controls.MouseSensitivity = float.NegativeInfinity;
            s.Accessibility.UiScale = float.NaN; s.Accessibility.ScreenShakeScale = float.NaN;
            s.Clamp();
            Assert.AreEqual(0.8f, s.Audio.MasterVolume); Assert.AreEqual(0.6f, s.Audio.MusicVolume); Assert.AreEqual(1f, s.Controls.MouseSensitivity);
            Assert.AreEqual(1f, s.Accessibility.UiScale); Assert.AreEqual(1f, s.Accessibility.ScreenShakeScale);
        }

        [Test]
        public void Autosave_Snaps()
        {
            int[,] cases = { { 7, 5 }, { 8, 10 }, { 12, 10 }, { 13, 15 }, { 22, 15 }, { 23, 30 }, { 99, 30 }, { -3, 0 }, { 0, 0 }, { 2, 0 }, { 3, 5 }, { 5, 5 }, { 10, 10 }, { 15, 15 }, { 30, 30 } };
            for (int i = 0; i < cases.GetLength(0); i++)
            {
                var g = new GameplaySettingsData { AutosaveIntervalMinutes = cases[i, 0] };
                g.Clamp();
                Assert.AreEqual(cases[i, 1], g.AutosaveIntervalMinutes, $"{cases[i, 0]} minutes");
            }
        }

        [Test]
        public void Clone_IsDeep()
        {
            var a = new GameSettings();
            a.Audio.MasterVolume = 0.3f; a.Controls.BindingOverridesJson = "{\"x\":1}"; a.Gameplay.AutosaveIntervalMinutes = 15;
            var b = a.Clone();
            Assert.AreNotSame(a.Audio, b.Audio); Assert.AreNotSame(a.Video, b.Video); Assert.AreNotSame(a.Controls, b.Controls);
            Assert.AreNotSame(a.Gameplay, b.Gameplay); Assert.AreNotSame(a.Accessibility, b.Accessibility);
            Assert.AreEqual(SettingsSection.None, a.DiffersFrom(b));
            b.Audio.MasterVolume = 0.9f; b.Gameplay.AutosaveIntervalMinutes = 30;
            Assert.AreEqual(0.3f, a.Audio.MasterVolume); Assert.AreEqual(15, a.Gameplay.AutosaveIntervalMinutes);
        }

        [Test]
        public void CopyFrom_OnlyFlaggedSections()
        {
            var src = new GameSettings();
            src.Audio.SfxVolume = 0.1f; src.Video.VSync = false; src.Controls.InvertCameraY = true; src.Gameplay.ShowControlHints = false; src.Accessibility.ReduceMotion = true;
            var dst = new GameSettings();
            dst.CopyFrom(src, SettingsSection.Audio);
            Assert.AreEqual(0.1f, dst.Audio.SfxVolume);
            Assert.AreEqual(SettingsSection.Audio, dst.DiffersFrom(new GameSettings()), "only audio changed");
            dst.CopyFrom(src, SettingsSection.Controls | SettingsSection.Accessibility);
            Assert.AreEqual(SettingsSection.Audio | SettingsSection.Controls | SettingsSection.Accessibility, dst.DiffersFrom(new GameSettings()));
            dst.ResetToDefaults(SettingsSection.Audio | SettingsSection.Controls);
            Assert.AreEqual(SettingsSection.Accessibility, dst.DiffersFrom(new GameSettings()));
        }

        [Test]
        public void DiffersFrom_ReportsSections()
        {
            var a = new GameSettings(); var b = new GameSettings();
            Assert.AreEqual(SettingsSection.None, a.DiffersFrom(b));
            b.Video.QualityLevel = 3; b.Gameplay.AutosaveIntervalMinutes = 10;
            Assert.AreEqual(SettingsSection.Video | SettingsSection.Gameplay, a.DiffersFrom(b));
            Assert.AreEqual(SettingsSection.Video | SettingsSection.Gameplay, b.DiffersFrom(a));
            b.ResetToDefaults(SettingsSection.All);
            Assert.AreEqual(SettingsSection.None, a.DiffersFrom(b));
        }
    }
}
