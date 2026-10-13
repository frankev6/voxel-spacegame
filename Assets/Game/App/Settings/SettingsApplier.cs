using System;
using Game.Core;
using Game.Core.Events;
using Game.Core.Settings;

namespace Game.App.Settings
{
    /// <summary>Pushes settings to their consumers (SPEC-01 §5.6): every section once at boot (ApplyAll, construction step 14), then only the
    /// sections in each SettingsChanged. Consumers that do not exist yet are skipped (Services.TryGet).</summary>
    public sealed class SettingsApplier : IDisposable
    {
        readonly ISettingsService settings;

        public SettingsApplier(ISettingsService settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            EventBus<SettingsChanged>.Subscribe(OnChanged);
        }

        public void Dispose() => EventBus<SettingsChanged>.Unsubscribe(OnChanged);

        void OnChanged(SettingsChanged e) => Apply(e.Sections, settings.Current);

        public static void ApplyAll(GameSettings s) => Apply(SettingsSection.All, s);

        public static void Apply(SettingsSection sections, GameSettings s)
        {
            if ((sections & SettingsSection.Video) != 0) ApplyVideo(s.Video);
            if ((sections & SettingsSection.Audio) != 0 && Game.Core.Services.TryGet<Game.Presentation.Audio.AudioService>(out var audio))
                audio.ApplyVolumes(SettingsMapping.ToVolumes(s.Audio));
            if ((sections & SettingsSection.Controls) != 0 && Game.Core.Services.TryGet<Game.Input.InputService>(out var input))
            {
                input.ApplyBindingOverrides(s.Controls.BindingOverridesJson);
                input.MouseSensitivity = s.Controls.MouseSensitivity;
                input.InvertCameraY = s.Controls.InvertCameraY;
                input.CameraZoomSensitivity = s.Controls.CameraZoomSensitivity;
            }
            if ((sections & (SettingsSection.Controls | SettingsSection.Accessibility)) != 0
                && Game.Core.Services.TryGet<Game.Presentation.Services.CameraRigService>(out var camera))
                camera.ApplySettings(new Game.Presentation.Data.CameraSettings
                {
                    MouseSensitivity = s.Controls.MouseSensitivity, InvertOrbitY = s.Controls.InvertCameraY,
                    YawMode = s.Controls.CameraYawModeIndex == 1 ? Game.Presentation.Data.CameraYawMode.Free : Game.Presentation.Data.CameraYawMode.FollowHeading,
                    ShakeScale = s.Accessibility.ScreenShakeScale, ReduceMotion = s.Accessibility.ReduceMotion
                });
            if ((sections & SettingsSection.Accessibility) != 0 && Game.Core.Services.TryGet<Game.UI.Services.UIRoot>(out var ui))
                ui.ApplyAccessibility(new Game.UI.Data.AccessibilityOptions
                {
                    UiScale = s.Accessibility.UiScale,
                    TextScale = (Game.UI.Data.TextScale)Math.Min((int)s.Accessibility.TextScaleIndex, 2),
                    ReduceMotion = s.Accessibility.ReduceMotion
                });
            // Gameplay: PERS-009 sets save.Autosave's interval from s.Gameplay.AutosaveIntervalMinutes.
        }

        static void ApplyVideo(VideoSettingsData v)
        {
            if (Game.Core.Services.TryGet<Game.Presentation.Vfx.FxRig>(out var fx))
                fx.SetQuality((Game.Data.Definitions.VfxQuality)v.VfxQuality);
            if (UnityEngine.Application.isBatchMode) return;   // headless runs and tests keep the project's screen and quality settings
            if (v.ResolutionWidth > 0 && v.ResolutionHeight > 0)
                UnityEngine.Screen.SetResolution(v.ResolutionWidth, v.ResolutionHeight, v.FullScreenMode,
                    new UnityEngine.RefreshRate { numerator = (uint)Math.Max(v.RefreshRateHz, 0), denominator = 1 });
            else UnityEngine.Screen.fullScreenMode = v.FullScreenMode;
            if (v.QualityLevel >= 0) UnityEngine.QualitySettings.SetQualityLevel(Math.Min(v.QualityLevel, UnityEngine.QualitySettings.names.Length - 1));
            UnityEngine.QualitySettings.vSyncCount = v.VSync ? 1 : 0;
            UnityEngine.Application.targetFrameRate = v.TargetFrameRate;
        }
    }
}
