using System;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 HUD-005: five authored tabs. HUD-021: the Controls page groups its binding rows under sub-tabs by action map and
    /// lays them out in two columns (about sixty rows in all, so they are built once rather than virtualized).</summary>
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
        TabBar tabs, controlsTabs;
        DropdownField resolution, fullscreen, quality, yaw, textScale;
        VisualElement rebinds;
        VisualTreeAsset rowTemplate;
        readonly System.Collections.Generic.List<TemplateContainer> rows = new System.Collections.Generic.List<TemplateContainer>(64);
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
            controlsTabs = Q<TabBar>("settings-controls-tabs");
            var groupNames = new string[SettingsViewModel.ControlsGroupLabels.Length];
            for (int i = 0; i < groupNames.Length; i++) groupNames[i] = "settings-controls-tab-" + SettingsViewModel.ControlsGroupMaps[i].ToLowerInvariant();
            controlsTabs.SetTabs(SettingsViewModel.ControlsGroupLabels, groupNames); controlsTabs.SelectionChanged += commands.SelectControlsGroup;
            rebinds = Q<VisualElement>("settings-rebinds");
            // The UXML carries one RebindRow instance only to hand its template to code; rows are made from it per binding.
            var carrier = Q<TemplateContainer>("settings-rebind-template");
            rowTemplate = carrier.templateSource ?? throw new InvalidOperationException("settings: missing rebind row template");
            carrier.RemoveFromHierarchy();
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
            controlsTabs.SelectedIndex = viewModel.ControlsGroup;
            for (int i = 0; i < pages.Length; i++) pages[i].EnableInClassList(UIStyleClasses.IsHidden, i != viewModel.SelectedTab);
            sliders[0].SetValueWithoutNotify(viewModel.MasterVolume); sliders[1].SetValueWithoutNotify(viewModel.MusicVolume);
            sliders[2].SetValueWithoutNotify(viewModel.SfxVolume); sliders[3].SetValueWithoutNotify(viewModel.UiVolume);
            sliders[4].SetValueWithoutNotify(viewModel.MouseSensitivity); sliders[5].SetValueWithoutNotify(viewModel.ScreenShake);
            sliders[6].SetValueWithoutNotify(viewModel.UiScale);
            toggles[0].SetValueWithoutNotify(viewModel.VSync); toggles[1].SetValueWithoutNotify(viewModel.InvertOrbitY); toggles[2].SetValueWithoutNotify(viewModel.ReduceMotion);
            resolution.choices = viewModel.Resolutions; quality.choices = viewModel.QualityNames;
            SetIndex(resolution, viewModel.ResolutionIndex); SetIndex(fullscreen, viewModel.FullscreenModeIndex);
            SetIndex(quality, viewModel.QualityIndex); SetIndex(yaw, viewModel.CameraYawModeIndex); SetIndex(textScale, viewModel.TextScaleIndex);
            RefreshRows();
        }

        /// <summary>One row per binding, made once for the list's length; each keeps its global index (the button is
        /// settings-rebind-&lt;index&gt;), and only the selected group's rows show.</summary>
        void RefreshRows()
        {
            var items = viewModel.Rebinds;
            while (rows.Count > items.Count) { rows[rows.Count - 1].RemoveFromHierarchy(); rows.RemoveAt(rows.Count - 1); }
            while (rows.Count < items.Count)
            {
                var row = rowTemplate.Instantiate(); row.AddToClassList("settings-rebind-cell"); row.userData = rows.Count;
                row.Q<Button>("settings-rebind-button").clicked += () =>
                { if (row.userData is int index) { if (index == viewModel.ListeningRow) commands.CancelRebind(); else commands.StartRebind(index); } };
                rows.Add(row); rebinds.Add(row);
            }
            int group = viewModel.ControlsGroup;
            string prefix = group >= 0 && group < SettingsViewModel.ControlsGroupMaps.Length ? SettingsViewModel.ControlsGroupMaps[group] + "/" : "";
            for (int i = 0; i < items.Count; i++)
            {
                var row = rows[i]; var item = items[i];
                row.dataSource = item;
                row.Q<Label>("settings-rebind-action").text = item.DisplayName;
                row.Q<Label>("settings-rebind-binding").text = item.BindingText;
                var button = row.Q<Button>(); button.name = "settings-rebind-" + i; button.text = item.IsListening ? "Cancel" : "Rebind";
                row.EnableInClassList(UIStyleClasses.IsHidden, item.ActionPath == null || !item.ActionPath.StartsWith(prefix, StringComparison.Ordinal));
            }
        }
        void SetIndex(DropdownField field, int index)
        { if (index >= 0 && index < field.choices.Count) field.SetValueWithoutNotify(field.choices[index]); }
    }
}
