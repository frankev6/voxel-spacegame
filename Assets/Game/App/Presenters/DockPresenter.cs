using System;
using Game.App.Economy;
using Game.App.EditorHost;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Galaxy.Services;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;

namespace Game.App.Presenters
{
    /// <summary>HUD-019: navigation follows current docking/flow owners, not stale deferred event payloads.</summary>
    internal sealed class DockPresenter : IDockCommands, IDisposable
    {
        readonly UIRoot ui;
        readonly DockViewModel model;
        readonly DockingService docking;
        readonly GalaxyService galaxy;
        readonly MerchantDirectory directory;
        readonly IGameFlow flow;
        MerchantLauncher merchants;
        ShipEditorLauncher editor;
        bool bound, disposed, synchronizing;
        internal DockPresenter(UIRoot ui, DockViewModel model, DockingService docking, GalaxyService galaxy,
            MerchantDirectory directory, IGameFlow flow)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            this.model = model ?? throw new ArgumentNullException(nameof(model));
            this.docking = docking ?? throw new ArgumentNullException(nameof(docking));
            this.galaxy = galaxy ?? throw new ArgumentNullException(nameof(galaxy));
            this.directory = directory ?? throw new ArgumentNullException(nameof(directory));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
        }
        // Composition binds the functioning registered ECO-008 / EDT-023 owners.
        internal void BindLaunchers(MerchantLauncher merchants, ShipEditorLauncher editor)
        {
            if (disposed) throw new ObjectDisposedException(nameof(DockPresenter));
            this.merchants = merchants; this.editor = editor;
            if (bound) Synchronize();
        }
        internal void Bind()
        {
            if (disposed) throw new ObjectDisposedException(nameof(DockPresenter));
            if (bound) return;
            bound = true;
            EventBus<DockStateChanged>.Subscribe(OnDockChanged); EventBus<SessionReset>.Subscribe(OnReset);
            ui.Navigator.StackChanged += Synchronize; Synchronize();
        }
        internal void Unbind()
        {
            if (!bound) return;
            bound = false;
            EventBus<DockStateChanged>.Unsubscribe(OnDockChanged); EventBus<SessionReset>.Unsubscribe(OnReset);
            ui.Navigator.StackChanged -= Synchronize;
            if (ui.Navigator.IsOpen<DockView>()) ui.Navigator.Pop(ui.Navigator.Get<DockView>());
        }
        void OnDockChanged(DockStateChanged value) => Synchronize();
        void OnReset(SessionReset value) => Synchronize();
        bool CanShow => bound && !disposed && flow.HasActiveSession && flow.CurrentScene == GameScene.World &&
            !flow.IsTransitioning && docking.IsDocked;
        void Synchronize()
        {
            if (!bound || disposed || synchronizing) return;
            synchronizing = true;
            try
            {
                bool show = CanShow;
                model.StationName = show ? galaxy.GetBodyName(docking.DockedBody) : "";
                bool market = show && directory.FindForBody(docking.DockedBody) != null;
                model.CanTrade = market && merchants != null;
                model.TradeBlockedReason = !market ? "No market here" : merchants == null ? "Trading is unavailable." : "";
                model.CanEditShip = show && editor != null;
                model.EditBlockedReason = model.CanEditShip ? "" : "Ship editing is unavailable.";
                var view = ui.Navigator.Get<DockView>();
                if (show && !view.IsOpen) ui.Navigator.Push(view);
                else if (!show && view.IsOpen) ui.Navigator.Pop(view);
            }
            finally { synchronizing = false; }
        }
        public void Trade()
        {
            if (!CanShow) return;
            Synchronize();
            if (!model.CanTrade) return;
            if (!merchants.TryOpenForBody(docking.DockedBody, onClosed: null))
            { model.CanTrade = false; model.TradeBlockedReason = "No market here"; }
        }
        public void EditShip()
        {
            if (!CanShow) return;
            Synchronize();
            if (!model.CanEditShip) return;
            if (!editor.EditPlayerShip(out string reason))
            { model.CanEditShip = false; model.EditBlockedReason = reason ?? "Ship editing is unavailable."; }
        }
        public void Undock() { if (bound && !disposed) docking.Undock(); }
        public void Dispose()
        {
            if (disposed) return;
            Unbind(); disposed = true; merchants = null; editor = null;
        }
    }
}
