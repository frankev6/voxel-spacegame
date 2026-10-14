using System;
using Game.App.Flow;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Persistence.Services;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;

namespace Game.App.Presenters
{
    /// <summary>App-lifetime view-model identity and screen/presenter ownership (SPEC-61 §6).</summary>
    public sealed class UiComposition : IRootScreenSet, IDisposable
    {
        readonly UIRoot ui;
        readonly SceneFlow flow;
        readonly LoadingPresenter loadingPresenter;
        readonly SaveSlotsPresenter saveSlotsPresenter;
        readonly SettingsPresenter settingsPresenter;
        readonly Game.Presentation.Presenters.InventoryPresenter inventoryPresenter;
        readonly Game.Presentation.Presenters.MerchantPresenter merchantPresenter;
        MainMenuPresenter menuPresenter;
        PausePresenter pausePresenter;
        bool rootsBound, disposed;
        internal Game.Presentation.Bridges.DialogPresenter DialogPresenter { get; private set; }
        internal MainMenuViewModel MainMenuModel { get; } = new MainMenuViewModel();
        internal SaveSlotsViewModel SaveSlotsModel { get; } = new SaveSlotsViewModel();
        internal SettingsViewModel SettingsModel { get; } = new SettingsViewModel();
        internal PauseViewModel PauseModel { get; } = new PauseViewModel();
        internal HudViewModel HudModel { get; } = new HudViewModel();
        internal InventoryViewModel InventoryModel { get; } = new InventoryViewModel();
        internal MerchantViewModel MerchantModel { get; } = new MerchantViewModel();
        internal DialogViewModel DialogModel { get; } = new DialogViewModel();
        internal ShipLogViewModel ShipLogModel { get; } = new ShipLogViewModel();
        internal GalaxyMapViewModel GalaxyMapModel { get; } = new GalaxyMapViewModel();
        internal StarSystemMapViewModel StarSystemMapModel { get; } = new StarSystemMapViewModel();
        internal FleetViewModel FleetModel { get; } = new FleetViewModel();
        internal ShipEditorViewModel ShipEditorModel { get; } = new ShipEditorViewModel();
        internal DockViewModel DockModel { get; } = new DockViewModel();
        internal LoadingViewModel LoadingModel { get; } = new LoadingViewModel();

        public UiComposition(UIRoot ui)
        {
            this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
            flow = Game.Core.Services.Get<SceneFlow>();
            loadingPresenter = new LoadingPresenter(flow, LoadingModel);
            Loading = new LoadingView(LoadingModel);
            ui.Register(Loading);
            loadingPresenter.Bind();
            saveSlotsPresenter = new SaveSlotsPresenter(ui, SaveSlotsModel, Game.Core.Services.Get<SaveService>(), Game.Core.Services.Get<IGameFlow>());
            ui.Register(new SaveSlotsView(SaveSlotsModel, saveSlotsPresenter));
            saveSlotsPresenter.Bind();
            settingsPresenter = new SettingsPresenter(ui, SettingsModel,
                Game.Core.Services.Get<Game.App.Settings.SettingsService>(), Game.Core.Services.Get<Game.Input.InputService>());
            ui.Register(new SettingsView(SettingsModel, settingsPresenter));
            settingsPresenter.Bind();
            pausePresenter = new PausePresenter(ui, PauseModel, Game.Core.Services.Get<IGameFlow>(), ShowSaveSlots, ShowSettings);
            ui.Register(new PauseView(PauseModel, pausePresenter));
            pausePresenter.Bind();
            var economy = Game.Core.Services.Get<Game.Economy.EconomyService>();
            inventoryPresenter = new Game.Presentation.Presenters.InventoryPresenter(InventoryModel);
            inventoryPresenter.Bind(economy.Inventory, economy.Wallet);
            ui.Register(new InventoryView(InventoryModel, inventoryPresenter));
            merchantPresenter = new Game.Presentation.Presenters.MerchantPresenter(ui, MerchantModel, economy.Inventory, economy.Wallet);
            ui.Register(new MerchantView(MerchantModel, merchantPresenter));
            merchantPresenter.Bind();
            Game.Core.Services.Register(merchantPresenter);
            EventBus<SessionReset>.Subscribe(OnSessionReset);
        }
        public UIScreen MainMenu => ui.Navigator.Get<MainMenuView>();
        public UIScreen FlightHud => ui.Navigator.Get<FlightHudView>();
        public UIScreen ShipEditor => ui.Navigator.Get<ShipEditorView>();
        public UIScreen Loading { get; }

        internal void ShowInventory() => ui.Navigator.Push<InventoryView>();

        internal void ShowSettings() => settingsPresenter.Show();

        internal void BindDialog(Game.Narrative.Dialog.DialogService service, Game.Data.Services.ContentCatalog content, IUISoundSink sound)
        {
            CheckAlive();
            if (DialogPresenter != null) throw new InvalidOperationException("Dialog presenter is already installed.");
            DialogPresenter = new Game.Presentation.Bridges.DialogPresenter(service, content, DialogModel, ui.Navigator, sound);
        }

        internal void ShowSaveSlots(SaveSlotsMode mode) => saveSlotsPresenter.Show(mode);

        // Installed only with real screen/launcher navigation, never placeholder command implementations.
        internal void BindMenuNavigation(Action<SaveSlotsMode> showSlots, Action openLab, Action showSettings)
        {
            CheckAlive();
            if (menuPresenter != null) throw new InvalidOperationException("Menu navigation is already installed.");
            if (showSlots == null) throw new ArgumentNullException(nameof(showSlots));
            var session = Game.Core.Services.Get<IGameFlow>();
            var saves = Game.Core.Services.Get<SaveService>();
            var menu = new MainMenuPresenter(ui, MainMenuModel, session, saves,
                () => showSlots(SaveSlotsMode.Load), openLab, showSettings);
            ui.Register(new MainMenuView(MainMenuModel, menu));
            menuPresenter = menu;
            menu.Bind();
        }
        internal void BindSceneRoots(IHudRadarSource radar, Func<string, string> bindingDisplay, IShipEditorCommands editorCommands)
        {
            CheckAlive();
            if (rootsBound) throw new InvalidOperationException("Scene roots are already installed.");
            if (menuPresenter == null) throw new InvalidOperationException("Install menu navigation before scene roots.");
            if (radar == null) throw new ArgumentNullException(nameof(radar));
            if (bindingDisplay == null) throw new ArgumentNullException(nameof(bindingDisplay));
            if (editorCommands == null) throw new ArgumentNullException(nameof(editorCommands));
            ui.Register(new FlightHudView(HudModel, radar, bindingDisplay));
            ui.Register(new ShipEditorView(ShipEditorModel, editorCommands));
            flow.SetRootScreens(this);
            rootsBound = true;
        }
        void OnSessionReset(SessionReset value)
        {
            if (disposed) return;
            // Never replace view models: views and bindings retain these exact objects across sessions.
            menuPresenter?.Unbind();
            pausePresenter?.Unbind();
            menuPresenter?.Bind();
            pausePresenter?.Bind();
            saveSlotsPresenter.ResetSession();
            settingsPresenter.Unbind();
            settingsPresenter.Bind();
            loadingPresenter.Dispose();
            loadingPresenter.Bind();
            var economy = Game.Core.Services.Get<Game.Economy.EconomyService>();
            inventoryPresenter.Bind(economy.Inventory, economy.Wallet);
            merchantPresenter.ResetSession();
            // TODO(HUD-009/010/013/014/015/016/017/018/019): rebind the owning feature presenters
            // to the new session models here when those features install their production bindings.
        }
        internal void Tick()
        {
            if (disposed) return;
            menuPresenter?.Tick();
            pausePresenter?.Tick();
            loadingPresenter.Tick();
            saveSlotsPresenter.Tick();
        }
        void CheckAlive() { if (disposed) throw new ObjectDisposedException(nameof(UiComposition)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EventBus<SessionReset>.Unsubscribe(OnSessionReset);
            DialogPresenter?.Dispose();
            loadingPresenter.Dispose();
            saveSlotsPresenter.Dispose();
            settingsPresenter.Dispose();
            inventoryPresenter.Dispose();
            Game.Core.Services.Unregister(merchantPresenter);
            merchantPresenter.Dispose();
            pausePresenter?.Dispose();
            menuPresenter?.Dispose();
            if (rootsBound) flow.SetRootScreens(null);
        }
        // TODO(HUD-002): install real navigation/editor/HUD bindings before replacing the M1 adapter,
        // then run the four required root/session acceptance checks.
    }
}
