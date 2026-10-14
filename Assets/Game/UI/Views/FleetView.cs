using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 HUD-017: virtualized fleet and fixed-height stats, with current model bindings.</summary>
    public sealed class FleetView : UIScreen
    {
        readonly FleetViewModel viewModel;
        readonly IFleetCommands commands;
        readonly List<ShipRow> shipRows = new();
        readonly List<StatRow> statRows = new();
        readonly int[] selected = new int[1];
        ListView ships, stats;
        Label name, shipClass, blocked, empty;
        Button edit, activate;
        bool observing, refreshing;
        public FleetView(FleetViewModel vm, IFleetCommands commands)
        { viewModel = vm ?? throw new ArgumentNullException(nameof(vm)); this.commands = commands ?? throw new ArgumentNullException(nameof(commands)); }
        public override string Key => "fleet";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => true;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => ships;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel; ships = Q<ListView>("fleet-ship-list"); stats = Q<ListView>("fleet-stats");
            name = Q<Label>("fleet-detail-name"); shipClass = Q<Label>("fleet-detail-class"); blocked = Q<Label>("fleet-edit-blocked"); empty = Q<Label>("fleet-empty");
            edit = Q<Button>("fleet-edit"); activate = Q<Button>("fleet-set-active");
            var shipTemplate = ships.itemTemplate ?? throw new InvalidOperationException("Fleet ship row template is missing."); ships.itemTemplate = null;
            var statTemplate = stats.itemTemplate ?? throw new InvalidOperationException("Fleet stat row template is missing."); stats.itemTemplate = null;
            ships.makeItem = () => { var row = new ShipRow(shipTemplate.Instantiate()); shipRows.Add(row); return row.Root; };
            ships.bindItem = (element, index) => ((ShipRow)element.userData).Bind(viewModel.Ships[index], index, observing);
            ships.unbindItem = (element, _) => ((ShipRow)element.userData).Unbind();
            ships.destroyItem = element => { var row = (ShipRow)element.userData; row.Unbind(); shipRows.Remove(row); };
            stats.makeItem = () => { var row = new StatRow(statTemplate.Instantiate()); statRows.Add(row); return row.Root; };
            stats.bindItem = (element, index) => ((StatRow)element.userData).Bind(viewModel.DetailStats[index], index, observing);
            stats.unbindItem = (element, _) => ((StatRow)element.userData).Unbind();
            stats.destroyItem = element => { var row = (StatRow)element.userData; row.Unbind(); statRows.Remove(row); };
            ships.itemsSource = viewModel.Ships; stats.itemsSource = viewModel.DetailStats; stats.selectionType = SelectionType.None;
            ships.selectionChanged += _ => { if (observing && !refreshing) commands.Select(ships.selectedIndex); };
            edit.clicked += () => { if (observing && viewModel.CanEdit) commands.Edit(); };
            activate.clicked += () => { if (observing && viewModel.CanSetActive) commands.SetActive(); };
            Q<Button>("fleet-close").clicked += () => { if (observing) Close(); };
            Root.RegisterCallback<DetachFromPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root)) Stop(); });
            Root.RegisterCallback<AttachToPanelEvent>(evt => { if (ReferenceEquals(evt.target, Root) && IsOpen) Start(); });
        }
        public override void OnPush(object args) => Start();
        public override void OnPop() => Stop();
        void Start() { if (observing) return; observing = true; viewModel.propertyChanged += Changed; Refresh(); }
        void Stop()
        {
            if (!observing) return; observing = false; viewModel.propertyChanged -= Changed;
            foreach (var row in shipRows) row.Unbind(); foreach (var row in statRows) row.Unbind();
        }
        void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            if (!observing || refreshing) return; refreshing = true;
            try
            {
                name.text = viewModel.DetailName ?? ""; shipClass.text = viewModel.DetailClass ?? ""; blocked.text = viewModel.EditBlockedReason ?? "";
                edit.SetEnabled(viewModel.CanEdit); activate.SetEnabled(viewModel.CanSetActive);
                empty.EnableInClassList(UIStyleClasses.IsHidden, viewModel.Ships.Count != 0);
                selected[0] = viewModel.SelectedIndex;
                ships.SetSelectionWithoutNotify(viewModel.SelectedIndex >= 0 && viewModel.SelectedIndex < viewModel.Ships.Count ? selected : Array.Empty<int>());
                ships.RefreshItems(); stats.RefreshItems();
            }
            finally { refreshing = false; }
        }
        abstract class Row<T> where T : ViewModelBase
        {
            internal readonly VisualElement Root;
            protected T Model;
            bool observing;
            protected Row(VisualElement root) { Root = root; Root.userData = this; Root.pickingMode = PickingMode.Ignore; }
            internal void Bind(T model, int index, bool active)
            { Unbind(); Model = model; Root.dataSource = model; Name(index); if (active) { Model.propertyChanged += Changed; observing = true; } Refresh(); }
            internal void Unbind() { if (observing) Model.propertyChanged -= Changed; observing = false; Model = null; Root.dataSource = null; }
            void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
            protected abstract void Name(int index);
            protected abstract void Refresh();
        }
        sealed class ShipRow : Row<FleetShipRowViewModel>
        {
            readonly Label name, active;
            readonly Meter hull;
            internal ShipRow(VisualElement root) : base(root)
            { name = root.Q<Label>("fleet-ship-name"); active = root.Q<Label>("fleet-ship-active"); hull = root.Q<Meter>("fleet-ship-hull"); }
            protected override void Name(int index)
            { Root.name = "fleet-ship-row-" + index; name.name = Root.name + "-name"; hull.name = Root.name + "-hull"; active.name = Root.name + "-active"; }
            protected override void Refresh()
            {
                name.text = Model.Name; hull.SetFraction(Model.HullFraction); active.EnableInClassList(UIStyleClasses.IsHidden, !Model.IsActive);
                Root.EnableInClassList(UIStyleClasses.IsSelected, Model.IsSelected);
            }
        }
        sealed class StatRow : Row<StatRowViewModel>
        {
            readonly Label label, value;
            internal StatRow(VisualElement root) : base(root) { label = root.Q<Label>("stat-label"); value = root.Q<Label>("stat-value"); }
            protected override void Name(int index) { Root.name = "fleet-stat-row-" + index; label.name = Root.name + "-label"; value.name = Root.name + "-value"; }
            protected override void Refresh() { label.text = Model.Label; value.text = Model.Value; }
        }
    }
}
