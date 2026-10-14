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
    public sealed class StarSystemMapView : UIScreen
    {
        readonly StarSystemMapViewModel viewModel;
        readonly IStarSystemMapCommands commands;
        MapScreenInteraction interaction;
        Label selectedName, selectedInfo;
        Button primary;
        bool observing;
        public StarSystemMapView(StarSystemMapViewModel vm, IStarSystemMapCommands commands)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands)); }
        public override string Key => "system-map";
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
            Canvas = Q<MapCanvas>("system-map-canvas");
            Canvas.PointClicked += commands.Select;
            selectedName = Q<Label>("system-map-selected-name"); selectedInfo = Q<Label>("system-map-selected-info");
            primary = Q<Button>("system-map-set-waypoint"); primary.clicked += commands.SetWaypoint;
            Q<Button>("system-map-close").clicked += Close;
            Q<Button>("system-map-focus-player").clicked += commands.FocusPlayer;
            interaction = new MapScreenInteraction(Root, Key, Canvas, () => viewModel.Data, commands.FocusPlayer);
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
            primary.SetEnabled(viewModel.CanSetWaypoint);
            primary.EnableInClassList(UIStyleClasses.IsHidden, !viewModel.CanSetWaypoint);
        }
    }
}
