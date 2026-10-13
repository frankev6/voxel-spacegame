using System;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 HUD-005: five authored tabs and virtualized binding rows.</summary>
    public sealed class SettingsView : UIScreen
    {
        readonly SettingsViewModel viewModel;
        readonly ISettingsCommands commands;
        readonly string[] tabKeys = { "audio", "video", "controls", "gameplay", "accessibility" };
        readonly string[] labels = { "Audio", "Video", "Controls", "Gameplay", "Accessibility" };
        readonly SettingKey[] floats = { SettingKey.MasterVolume, SettingKey.MusicVolume, SettingKey.SfxVolume, SettingKey.UiVolume, SettingKey.MouseSensitivity, SettingKey.ScreenShake, SettingKey.UiScale };
        readonly string[] floatNames = { "master-volume", "music-volume", "sfx-volume", "ui-volume", "mouse-sensitivity", "screen-shake", "ui-scale" };
        readonly SettingKey[] bools = { SettingKey.VSync, SettingKey.InvertOrbitY, SettingKey.ReduceMotion };
        readonly string[] boolNames = { "vsync", "invert-orbit-y", "reduce-motion" };
        readonly Slider[] sliders = new Slider[7];
        readonly Toggle[] toggles = new Toggle[3];
        readonly VisualElement[] pages = new VisualElement[5];
        TabBar tabs;
        DropdownField resolution, fullscreen, quality, yaw, textScale;
        ListView rebinds;
        bool observing;
        public SettingsView(SettingsViewModel vm, ISettingsCommands commands)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands)); }
        public override string Key => "settings";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => tabs;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            tabs = Q<TabBar>("settings-tabs");
            var names = new string[5];
            for (int i = 0; i < names.Length; i++) { names[i] = "settings-tab-" + tabKeys[i]; pages[i] = Q<VisualElement>("settings-page-" + tabKeys[i]); }
            tabs.SetTabs(labels, names); tabs.SelectionChanged += commands.SelectTab;
            for (int i = 0; i < sliders.Length; i++)
            { var key = floats[i]; sliders[i] = Q<Slider>("settings-" + floatNames[i]); sliders[i].RegisterValueChangedCallback(e => commands.SetFloat(key, e.newValue)); }
            for (int i = 0; i < toggles.Length; i++)
            { var key = bools[i]; toggles[i] = Q<Toggle>("settings-" + boolNames[i]); toggles[i].RegisterValueChangedCallback(e => commands.SetBool(key, e.newValue)); }
            resolution = Dropdown("resolution", SettingKey.Resolution);
            fullscreen = Dropdown("fullscreen-mode", SettingKey.FullscreenMode);
            fullscreen.choices = new System.Collections.Generic.List<string> { "Exclusive fullscreen", "Borderless fullscreen", "Maximized window", "Windowed" };
            quality = Dropdown("quality", SettingKey.Quality);
            yaw = Dropdown("camera-yaw-mode", SettingKey.CameraYawMode); yaw.choices = new System.Collections.Generic.List<string> { "Follow heading", "Free orbit" };
            textScale = Dropdown("text-scale", SettingKey.TextScale); textScale.choices = new System.Collections.Generic.List<string> { "100%", "115%", "130%" };
            rebinds = Q<ListView>("settings-rebinds");
            var template = rebinds.itemTemplate ?? throw new InvalidOperationException("settings: missing rebind row template");
            rebinds.itemTemplate = null;
            rebinds.makeItem = () =>
            {
                var row = template.Instantiate();
                row.Q<Button>("settings-rebind-button").clicked += () => { if (row.userData is int index) { if (index == viewModel.ListeningRow) commands.CancelRebind(); else commands.StartRebind(index); } };
                return row;
            };
            rebinds.bindItem = (row, index) =>
            {
                row.userData = index; row.dataSource = viewModel.Rebinds[index];
                var item = viewModel.Rebinds[index];
                row.Q<Label>("settings-rebind-action").text = item.DisplayName;
                row.Q<Label>("settings-rebind-binding").text = item.BindingText;
                var button = row.Q<Button>(); button.name = "settings-rebind-" + index;
                button.text = item.IsListening ? "Cancel" : "Rebind";
                // One listener per pooled row, with the current row index read at activation.
            };
            rebinds.unbindItem = (row, _) => { row.userData = null; row.dataSource = null; };
            rebinds.itemsSource = viewModel.Rebinds;
            Q<Button>("settings-reset-bindings").clicked += commands.ResetBindings;
            Q<Button>("settings-close").clicked += () => { commands.CancelRebind(); Close(); };
        }
        DropdownField Dropdown(string name, SettingKey key)
        {
            var field = Q<DropdownField>("settings-" + name);
            field.RegisterValueChangedCallback(_ => commands.SetIndex(key, field.index)); return field;
        }
        public override void OnPush(object args)
        { if (!observing) { viewModel.propertyChanged += Changed; observing = true; } Refresh(); }
        public override void OnPop()
        { if (observing) { viewModel.propertyChanged -= Changed; observing = false; } commands.CancelRebind(); }
        public override bool OnBack()
        { if (viewModel.ListeningRow < 0) return false; commands.CancelRebind(); return true; }
        void Changed(object sender, BindablePropertyChangedEventArgs _) => Refresh();
        void Refresh()
        {
            tabs.SelectedIndex = viewModel.SelectedTab;
            for (int i = 0; i < pages.Length; i++) pages[i].EnableInClassList(UIStyleClasses.IsHidden, i != viewModel.SelectedTab);
            sliders[0].SetValueWithoutNotify(viewModel.MasterVolume); sliders[1].SetValueWithoutNotify(viewModel.MusicVolume);
            sliders[2].SetValueWithoutNotify(viewModel.SfxVolume); sliders[3].SetValueWithoutNotify(viewModel.UiVolume);
            sliders[4].SetValueWithoutNotify(viewModel.MouseSensitivity); sliders[5].SetValueWithoutNotify(viewModel.ScreenShake);
            sliders[6].SetValueWithoutNotify(viewModel.UiScale);
            toggles[0].SetValueWithoutNotify(viewModel.VSync); toggles[1].SetValueWithoutNotify(viewModel.InvertOrbitY); toggles[2].SetValueWithoutNotify(viewModel.ReduceMotion);
            resolution.choices = viewModel.Resolutions; quality.choices = viewModel.QualityNames;
            SetIndex(resolution, viewModel.ResolutionIndex); SetIndex(fullscreen, viewModel.FullscreenModeIndex);
            SetIndex(quality, viewModel.QualityIndex); SetIndex(yaw, viewModel.CameraYawModeIndex); SetIndex(textScale, viewModel.TextScaleIndex);
            rebinds.RefreshItems();
        }
        void SetIndex(DropdownField field, int index)
        { if (index >= 0 && index < field.choices.Count) field.SetValueWithoutNotify(field.choices[index]); }
    }
}
