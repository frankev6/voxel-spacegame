using System;
using System.Collections;
using Game.App.Economy;
using Game.App.EditorHost;
using Game.App.Presenters;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Economy;
using Game.Data.Definitions;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.Tests.Common;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>HUD-019: production flow/UI/docking and the actual ECO-008/EDT-023 launcher bindings.</summary>
    public sealed class LoadingDockViewTests : GameFlowFixture
    {
        sealed class DockScreen : IDisposable
        {
            internal DockViewModel Model => (DockViewModel)View.Root.dataSource;
            internal readonly DockPresenter Presenter;
            internal readonly DockView View;
            internal DockScreen()
            {
                // Composition owns this presenter and its launcher bindings. Never replace a
                // missing production registration with a separately constructed test presenter.
                Presenter = Services.Get<DockPresenter>();
                View = Services.Get<UIRoot>().Navigator.Get<DockView>();
            }
            public void Dispose() { }
        }
        BodyId Station()
        {
            Assert.IsTrue(Service<GalaxyService>().TryResolveBody(new ContentId("starsys_ark_start"), "ark_station", out var body));
            return body;
        }
        [UnityTest] public IEnumerator Loading_EscIgnored_ContextNone_HudHidden()
        {
            var ui = Service<UIRoot>(); var previous = ui.Navigator.Top;
            var loading = ui.Navigator.Get<LoadingView>();
            ui.Navigator.SetRoot(loading);
            try
            {
                ui.HandleBackPressed(); yield return null;
                Assert.AreSame(loading, ui.Navigator.Top); Assert.IsTrue(loading.IsOpen);
                Assert.AreEqual(InputContext.None, ui.Navigator.CurrentContext);
                Assert.IsTrue(ui.Navigator.WorldPointerBlocked);
                Assert.IsFalse(ui.IsWorldPointerAllowed(new Vector2(100, 100)));
                Assert.IsTrue(ui.GetLayer(UILayer.Hud).ClassListContains(UIStyleClasses.IsHidden));
                Assert.AreEqual("Loading…", loading.Root.Q<Label>("loading-status").text);
            }
            finally { ui.Navigator.SetRoot(previous); }
            Assert.AreEqual(0, Service<IPauseService>().ActiveTokenCount);
        }
        [UnityTest] public IEnumerator Dock_PushOnDock_PopOnUndock()
        {
            yield return NewGame(7);
            var ui = Service<UIRoot>(); var docking = Service<DockingService>(); var station = Station();
            int subscribers = EventBus<DockStateChanged>.SubscriberCount;
            using var screen = new DockScreen();
            Assert.AreEqual(subscribers, EventBus<DockStateChanged>.SubscriberCount);
            int baseline = ui.Navigator.Count;
            docking.Dock(station); EventHub.DrainAll(); docking.Dock(station); EventHub.DrainAll();
            Assert.AreSame(screen.View, ui.Navigator.Top); Assert.AreEqual(baseline + 1, ui.Navigator.Count);
            Assert.AreEqual(Service<GalaxyService>().GetBodyName(station), screen.View.Root.Q<Label>("dock-station-name").text);
            Assert.AreEqual(InputContext.Menu, ui.Navigator.CurrentContext); Assert.AreEqual(0, Service<IPauseService>().ActiveTokenCount);
            Assert.IsTrue(screen.Model.CanTrade); Assert.AreEqual("", screen.Model.TradeBlockedReason);
            ui.HandleBackPressed(); Assert.IsFalse(docking.IsDocked); EventHub.DrainAll(); Assert.IsFalse(screen.View.IsOpen);
            Assert.AreEqual(InputContext.Flight, ui.Navigator.CurrentContext);
            docking.Dock(station); EventHub.DrainAll();
            DialogTestContext.Activate(screen.View.Root.Q<Button>("dock-undock")); EventHub.DrainAll();
            Assert.IsFalse(docking.IsDocked); Assert.IsFalse(screen.View.IsOpen);
            // Deferred notifications consult current state, so dock-then-undock never leaves a stale screen.
            docking.Dock(station); docking.Undock(); EventHub.DrainAll(); Assert.IsFalse(screen.View.IsOpen);
            screen.Dispose(); Assert.AreEqual(subscribers, EventBus<DockStateChanged>.SubscriberCount);
        }
        [UnityTest] public IEnumerator Dock_Trade_OpensMerchant()
        {
            Assert.IsNotNull(Service<MerchantLauncher>());
            yield return NewGame(7);
            using var screen = new DockScreen();
            var docking = Service<DockingService>(); docking.Dock(Station()); EventHub.DrainAll();
            Assert.IsTrue(screen.Model.CanTrade);
            DialogTestContext.Activate(screen.View.Root.Q<Button>("dock-trade"));
            var ui = Service<UIRoot>(); Assert.IsTrue(ui.Navigator.Top is MerchantView);
            Assert.IsNotNull(Service<EconomyService>().Merchants.Current);
            ui.HandleBackPressed(); Assert.AreSame(screen.View, ui.Navigator.Top);
            Assert.IsNull(Service<EconomyService>().Merchants.Current);
            screen.Presenter.Undock(); EventHub.DrainAll();
        }
        [UnityTest] public IEnumerator Dock_NoMerchant_TradeDisabled()
        {
            yield return NewGame(7);
            using var screen = new DockScreen(); var galaxy = Service<GalaxyService>();
            var body = new BodyId(galaxy.State.CurrentStarSystem, BodyRole.Star, 0, 0);
            Assert.IsNull(Service<MerchantDirectory>().FindForBody(body));
            Service<DockingService>().Dock(body); EventHub.DrainAll();
            Assert.IsTrue(screen.View.IsOpen); Assert.IsFalse(screen.Model.CanTrade);
            Assert.AreEqual("No market here", screen.View.Root.Q<Label>("dock-trade-blocked").text);
            Assert.IsFalse(screen.View.Root.Q<Button>("dock-trade").enabledSelf);
            screen.Presenter.Trade(); Assert.IsFalse(Service<UIRoot>().Navigator.IsOpen<MerchantView>());
            Assert.IsTrue(screen.Model.CanEditShip); Assert.AreEqual("", screen.Model.EditBlockedReason);
            screen.Presenter.Undock(); EventHub.DrainAll(); Assert.IsFalse(screen.View.IsOpen);
        }
    }
}
