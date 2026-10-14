using System;
using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>HUD-019: live dock actions and explicit owner-provided availability reasons.</summary>
    public sealed class DockView : UIScreen
    {
        readonly DockViewModel viewModel;
        readonly IDockCommands commands;
        Label station, tradeReason, editReason;
        Button trade, edit, undock;
        bool observing;

        public DockView(DockViewModel vm, IDockCommands commands)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public override string Key => "dock";
        public override UILayer Layer => UILayer.Screens;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => true;
        public override bool IsRoot => false;
        public override VisualElement DefaultFocus => undock;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel;
            station = Q<Label>("dock-station-name"); tradeReason = Q<Label>("dock-trade-blocked"); editReason = Q<Label>("dock-edit-blocked");
            trade = Q<Button>("dock-trade"); edit = Q<Button>("dock-edit-ship"); undock = Q<Button>("dock-undock");
            trade.clicked += commands.Trade; edit.clicked += commands.EditShip; undock.clicked += commands.Undock;
        }
        public override void OnPush(object args)
        {
            if (!observing) { viewModel.propertyChanged += Changed; observing = true; }
            Refresh();
        }
        public override void OnPop() { if (observing) { viewModel.propertyChanged -= Changed; observing = false; } }
        public override void OnRevealed() => Refresh();
        public override bool OnBack() { commands.Undock(); return true; }
        void Changed(object sender, BindablePropertyChangedEventArgs args) => Refresh();
        void Refresh()
        {
            station.text = viewModel.StationName;
            trade.SetEnabled(viewModel.CanTrade); edit.SetEnabled(viewModel.CanEditShip);
            trade.tooltip = viewModel.TradeBlockedReason; edit.tooltip = viewModel.EditBlockedReason;
            tradeReason.text = viewModel.TradeBlockedReason; editReason.text = viewModel.EditBlockedReason;
            tradeReason.EnableInClassList(UIStyleClasses.IsHidden, viewModel.CanTrade);
            editReason.EnableInClassList(UIStyleClasses.IsHidden, viewModel.CanEditShip);
        }
    }
}
