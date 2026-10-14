using System;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>HUD-016: retained map, current selection and owner-backed commands.</summary>
    public sealed class GalaxyMapView : UIScreen
    {
        readonly GalaxyMapViewModel viewModel;
        readonly IGalaxyMapCommands commands;
        MapScreenInteraction interaction;
        Label selectedName, selectedInfo, reason;
        Button primary;
        bool observing;
        public GalaxyMapView(GalaxyMapViewModel vm, IGalaxyMapCommands commands)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands)); }
        public override string Key => "galaxy-map";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => Canvas;
        public MapCanvas Canvas { get; private set; }
        public event Action<object> Pushed;
        public event Action Popped;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            Canvas = Q<MapCanvas>("galaxy-map-canvas");
            Canvas.PointClicked += commands.Select;
            selectedName = Q<Label>("galaxy-map-selected-name"); selectedInfo = Q<Label>("galaxy-map-selected-info");
            primary = Q<Button>("galaxy-map-set-destination"); primary.clicked += commands.SetDestination;
            Q<Button>("galaxy-map-close").clicked += Close;
            Q<Button>("galaxy-map-focus-current").clicked += commands.FocusCurrent;
            Q<Button>("galaxy-map-clear-destination").clicked += commands.ClearDestination;
            reason = Q<Label>("galaxy-map-destination-blocked");
            interaction = new MapScreenInteraction(Root, Key, Canvas, () => viewModel.Data, commands.FocusCurrent);
            Refresh();
        }
        public void FocusMap(double2 center, bool frameBounds = true) => interaction.Focus(center, frameBounds);
        public override void OnPush(object args)
        {
            if (!observing) { observing = true; viewModel.propertyChanged += Changed; }
            Pushed?.Invoke(args); Refresh();
        }
        public override void OnPop()
        { if (observing) { observing = false; viewModel.propertyChanged -= Changed; } Popped?.Invoke(); }
        void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            Canvas.SetData(viewModel.Data, viewModel.DataVersion);
            selectedName.text = viewModel.SelectedName ?? ""; selectedInfo.text = viewModel.SelectedInfo ?? "";
            primary.SetEnabled(viewModel.CanSetDestination);
            reason.text = viewModel.DestinationBlockedReason ?? "";
            reason.EnableInClassList(UIStyleClasses.IsHidden, string.IsNullOrEmpty(reason.text));
            primary.tooltip = viewModel.DestinationBlockedReason ?? "";
        }
    }
}
