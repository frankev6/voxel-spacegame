using System;
using System.Collections.Generic;
using Game.App.Settings;
using Game.Core.Events;
using Game.Core.Settings;
using Game.Input;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.App.Presenters
{
    /// <summary>SPEC-61 HUD-005: persistent settings and real binding operations, with app-owned model lifetime.</summary>
    public sealed class SettingsPresenter : ISettingsCommands, IDisposable
    {
        readonly UIRoot ui;
        readonly SettingsViewModel model;
        readonly SettingsService settings;
        readonly InputService input;
        readonly List<(InputAction action, int index)> bindings = new(64);
        readonly List<(int width, int height, int hz)> resolutions = new(32);
        bool bound, disposed;
        public SettingsPresenter(UIRoot ui, SettingsViewModel model, SettingsService settings, InputService input)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.input = input ?? throw new ArgumentNullException(nameof(input));
        }
        public void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(SettingsPresenter));
            if (bound) return;
            bound = true; EventBus<SettingsChanged>.Subscribe(Changed);
            input.BindingsChanged += RefreshBindings; ui.Navigator.StackChanged += StackChanged;
            BuildOptions(); BuildBindings(); Refresh();
        }
        public void Unbind()
        {
            if (!bound) return;
            CancelRebind(); bound = false; EventBus<SettingsChanged>.Unsubscribe(Changed);
            input.BindingsChanged -= RefreshBindings; ui.Navigator.StackChanged -= StackChanged;
        }
        public void Show()
        {
            if (!bound || disposed) return;
            Refresh(); if (!ui.Navigator.IsOpen<SettingsView>()) ui.Navigator.Push<SettingsView>();
        }
        void StackChanged() { if (ui.Navigator.Top is SettingsView) Refresh(); else CancelRebind(); }
        void Changed(SettingsChanged _) => Refresh();
        void BuildOptions()
        {
            model.Resolutions.Clear(); resolutions.Clear();
            AddResolution(0, 0, 0, "Current display");
            foreach (var value in Screen.resolutions)
            {
                int hz = Mathf.RoundToInt((float)value.refreshRateRatio.value);
                if (!resolutions.Contains((value.width, value.height, hz)))
                    AddResolution(value.width, value.height, hz, value.width + " × " + value.height + " · " + hz + " Hz");
            }
            var video = settings.Current.Video;
            if (video.ResolutionWidth != 0 && !resolutions.Contains((video.ResolutionWidth, video.ResolutionHeight, video.RefreshRateHz)))
                AddResolution(video.ResolutionWidth, video.ResolutionHeight, video.RefreshRateHz, video.ResolutionWidth + " × " + video.ResolutionHeight + " · saved");
            model.NotifyResolutions();
            model.QualityNames.Clear(); model.QualityNames.Add("Project default"); model.QualityNames.AddRange(QualitySettings.names); model.NotifyQualityNames();
        }
        void AddResolution(int width, int height, int hz, string label)
        { resolutions.Add((width, height, hz)); model.Resolutions.Add(label); }
        void BuildBindings()
        {
            bindings.Clear(); model.Rebinds.Clear();
            foreach (var map in input.Controls.asset.actionMaps)
            {
                if (map.name != "Global" && map.name != "Flight" && map.name != "Editor" && map.name != "Dialog") continue;
                foreach (var action in map.actions)
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i]; string path = binding.path ?? "";
                        if (binding.isComposite || binding.isPartOfComposite || path.EndsWith("/position", StringComparison.Ordinal) ||
                            path.EndsWith("/delta", StringComparison.Ordinal) || path.EndsWith("/scroll", StringComparison.Ordinal)) continue;
                        bindings.Add((action, i));
                        model.Rebinds.Add(new RebindRowViewModel { ActionPath = map.name + "/" + action.name,
                            DisplayName = map.name + " / " + action.name });
                    }
            }
            RefreshBindings();
        }
        void RefreshBindings()
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                var target = bindings[i]; var row = model.Rebinds[i];
                row.IsListening = i == model.ListeningRow;
                row.BindingText = row.IsListening ? "Press a key…" : target.action.GetBindingDisplayString(target.index);
            }
            model.NotifyRebinds();
        }
        public void Refresh()
        {
            if (disposed) return;
            var data = settings.Current;
            model.MasterVolume = data.Audio.MasterVolume; model.MusicVolume = data.Audio.MusicVolume;
            model.SfxVolume = data.Audio.SfxVolume; model.UiVolume = data.Audio.UiVolume;
            model.ResolutionIndex = Math.Max(0, resolutions.IndexOf((data.Video.ResolutionWidth, data.Video.ResolutionHeight, data.Video.RefreshRateHz)));
            model.FullscreenModeIndex = (int)data.Video.FullScreenMode; model.VSync = data.Video.VSync;
            model.QualityIndex = Mathf.Clamp(data.Video.QualityLevel + 1, 0, model.QualityNames.Count - 1);
            model.MouseSensitivity = data.Controls.MouseSensitivity; model.InvertOrbitY = data.Controls.InvertCameraY;
            model.CameraYawModeIndex = data.Controls.CameraYawModeIndex;
            model.ScreenShake = data.Accessibility.ScreenShakeScale; model.UiScale = data.Accessibility.UiScale;
            model.TextScaleIndex = data.Accessibility.TextScaleIndex; model.ReduceMotion = data.Accessibility.ReduceMotion;
            RefreshBindings();
        }
        public void SelectTab(int tab) { if (bound && !disposed && tab >= 0 && tab < 5 && tab != model.SelectedTab) { CancelRebind(); model.SelectedTab = tab; } }
        void Apply(GameSettings edited, SettingsSection section)
        {
            if (!bound || disposed) return;
            settings.Apply(edited, section);
            if ((section & SettingsSection.Accessibility) != 0)
            {
                var value = settings.Current.Accessibility;
                ui.ApplyAccessibility(new AccessibilityOptions { UiScale = value.UiScale, TextScale = (TextScale)value.TextScaleIndex, ReduceMotion = value.ReduceMotion });
            }
            try { settings.Flush(); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException)
            { ui.Notifications.Post(new Notification { Kind = NotificationKind.Danger, Label = "Settings could not be saved: " + e.Message }); }
            Refresh();
        }
        public void SetFloat(SettingKey key, float value)
        {
            var edited = settings.CreateEditableCopy(); SettingsSection section;
            switch (key)
            {
                case SettingKey.MasterVolume: edited.Audio.MasterVolume = value; section = SettingsSection.Audio; break;
                case SettingKey.MusicVolume: edited.Audio.MusicVolume = value; section = SettingsSection.Audio; break;
                case SettingKey.SfxVolume: edited.Audio.SfxVolume = value; section = SettingsSection.Audio; break;
                case SettingKey.UiVolume: edited.Audio.UiVolume = value; section = SettingsSection.Audio; break;
                case SettingKey.MouseSensitivity: edited.Controls.MouseSensitivity = value; section = SettingsSection.Controls; break;
                case SettingKey.ScreenShake: edited.Accessibility.ScreenShakeScale = value; section = SettingsSection.Accessibility; break;
                case SettingKey.UiScale: edited.Accessibility.UiScale = value; section = SettingsSection.Accessibility; break;
                default: return;
            }
            Apply(edited, section);
        }
        public void SetBool(SettingKey key, bool value)
        {
            var edited = settings.CreateEditableCopy(); SettingsSection section;
            switch (key)
            {
                case SettingKey.VSync: edited.Video.VSync = value; section = SettingsSection.Video; break;
                case SettingKey.InvertOrbitY: edited.Controls.InvertCameraY = value; section = SettingsSection.Controls; break;
                case SettingKey.ReduceMotion: edited.Accessibility.ReduceMotion = value; section = SettingsSection.Accessibility; break;
                default: return;
            }
            Apply(edited, section);
        }
        public void SetIndex(SettingKey key, int index)
        {
            var edited = settings.CreateEditableCopy(); SettingsSection section;
            switch (key)
            {
                case SettingKey.Resolution:
                    if (index < 0 || index >= resolutions.Count) return;
                    var resolution = resolutions[index]; edited.Video.ResolutionWidth = resolution.width;
                    edited.Video.ResolutionHeight = resolution.height; edited.Video.RefreshRateHz = resolution.hz; section = SettingsSection.Video; break;
                case SettingKey.FullscreenMode:
                    if (index < 0 || index > 3) return;
                    edited.Video.FullScreenMode = (FullScreenMode)index; section = SettingsSection.Video; break;
                case SettingKey.Quality:
                    if (index < 0 || index >= model.QualityNames.Count) return;
                    edited.Video.QualityLevel = index - 1; section = SettingsSection.Video; break;
                case SettingKey.TextScale:
                    if (index < 0 || index > 2) return;
                    edited.Accessibility.TextScaleIndex = (byte)index; section = SettingsSection.Accessibility; break;
                case SettingKey.CameraYawMode:
                    if (index < 0 || index > 1) return;
                    edited.Controls.CameraYawModeIndex = (byte)index; section = SettingsSection.Controls; break;
                default: return;
            }
            Apply(edited, section);
        }
        public void StartRebind(int row)
        {
            if (!bound || disposed || ui.Navigator.Top is not SettingsView || row < 0 || row >= bindings.Count) return;
            CancelRebind(); model.ListeningRow = row; RefreshBindings();
            var target = bindings[row];
            input.StartRebind(model.Rebinds[row].ActionPath, target.index, result =>
            {
                model.ListeningRow = -1;
                if (result.Status == RebindStatus.Completed)
                {
                    var edited = settings.CreateEditableCopy(); edited.Controls.BindingOverridesJson = input.SaveBindingOverridesJson();
                    Apply(edited, SettingsSection.Controls);
                }
                else if (result.Status == RebindStatus.Conflict || result.Status == RebindStatus.Failed)
                    ui.Notifications.Post(new Notification { Kind = NotificationKind.Warning, Label = result.Status == RebindStatus.Conflict ? "Already used by " + result.ConflictActionPath : "This binding could not be changed." });
                RefreshBindings();
            });
        }
        public void CancelRebind()
        {
            if (disposed) return;
            // InputService is shared: only cancel the operation this presenter started.
            if (model.ListeningRow >= 0) input.CancelRebind();
            model.ListeningRow = -1; RefreshBindings();
        }
        public void ResetBindings()
        {
            if (!bound || disposed) return;
            CancelRebind(); input.ResetAllBindings(); var edited = settings.CreateEditableCopy();
            edited.Controls.BindingOverridesJson = input.SaveBindingOverridesJson(); Apply(edited, SettingsSection.Controls);
        }
        public void Dispose() { if (disposed) return; Unbind(); disposed = true; bindings.Clear(); resolutions.Clear(); }
    }
}
