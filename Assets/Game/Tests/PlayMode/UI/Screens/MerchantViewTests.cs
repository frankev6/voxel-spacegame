using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Presentation.Presenters;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>HUD-012: four required cases, real economy owners and bounded runtime panel frames.</summary>
    public sealed class MerchantViewTests
    {
        sealed class ClosingModal : UIScreen
        {
            readonly Action closed;
            internal ClosingModal(Action closed) => this.closed = closed;
            public override string Key => "merchant-test-modal";
            public override UILayer Layer => UILayer.Modals;
            public override VisualElement CreateView() => new VisualElement();
            protected override void OnBuild() { }
            public override void OnPop() => closed();
        }
        sealed class Market : IDisposable
        {
            readonly List<Object> owned = new();
            internal readonly List<ItemDefinition> Items = new();
            internal readonly MerchantDefinition Merchant;
            internal readonly InventoryModel Inventory;
            internal readonly Wallet Wallet = new();
            internal readonly MerchantService Service;
            internal Market(int stockCount = 2, int cargoCount = 0)
            {
                var definitions = new List<ContentDefinition>
                {
                    AssetDatabase.LoadAssetAtPath<BlockDefinition>("Assets/Game/Content/Blocks/block_missing.asset"),
                    AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/Game/Content/Items/item_missing.asset")
                };
                for (int i = 0; i < Math.Max(stockCount, cargoCount); i++)
                {
                    int index = i;
                    var item = Make<ItemDefinition>("item_hud012_" + i, so =>
                    {
                        so.FindProperty("displayName").stringValue = new string('N', 40) + " " + index;
                        so.FindProperty("description").stringValue = string.Join(" ", Enumerable.Repeat("A long cargo description.", 40));
                        so.FindProperty("category").intValue = index == 1 ? (int)ItemCategory.Module : (int)ItemCategory.Resource;
                        so.FindProperty("basePrice").intValue = 100; so.FindProperty("maxStack").intValue = 999;
                    });
                    Items.Add(item); definitions.Add(item);
                }
                Merchant = Make<MerchantDefinition>("merchant_hud012", so =>
                {
                    so.FindProperty("displayName").stringValue = new string('M', 40);
                    var rows = so.FindProperty("stock"); rows.arraySize = stockCount;
                    for (int i = 0; i < stockCount; i++)
                    {
                        var row = rows.GetArrayElementAtIndex(i); row.FindPropertyRelative("item").objectReferenceValue = Items[i];
                        row.FindPropertyRelative("count").intValue = 5; row.FindPropertyRelative("priceFactor").floatValue = 1;
                    }
                });
                var config = Make<EconomyConfigDefinition>("config_economy_hud012");
                definitions.Add(Merchant); definitions.Add(config);
                var report = new ValidationReport("HUD-012 market"); var catalog = ContentCatalog.Build(definitions, report);
                Assert.IsFalse(report.HasErrors, report.ToText());
                Inventory = new InventoryModel(catalog); Inventory.SetCapacity(InventoryModel.MaxSlots); Wallet.Set(10000);
                for (int i = 0; i < cargoCount; i++) Assert.IsTrue(Inventory.TryAdd(Items[i], 1, AcquireSource.NewGame, out _));
                Service = new MerchantService(Inventory, Wallet, catalog, config, () => 0);
            }
            T Make<T>(string id, Action<SerializedObject> edit = null) where T : ContentDefinition
            {
                var value = ScriptableObject.CreateInstance<T>(); owned.Add(value);
                using var so = new SerializedObject(value); so.FindProperty("id").stringValue = id;
                edit?.Invoke(so); so.ApplyModifiedPropertiesWithoutUndo(); return value;
            }
            internal MerchantSession Open(bool refused = false) => Service.OpenSession(Merchant, 0, refused, "Hostile market");
            public void Dispose() { Service.ResetToEmpty(); foreach (var value in owned) Object.Destroy(value); }
        }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly DialogTestContext.Pause Tokens = new();
            internal readonly DialogTestContext.Context Target = new();
            internal readonly UIRoot UI;
            internal readonly MerchantViewModel Model = new();
            internal readonly MerchantPresenter Presenter;
            internal readonly MerchantView View;
            readonly UIConfig config;
            internal Window(Market market, int width = 1280, int height = 720)
            {
                Panel = new UITestPanel(width, height);
                config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
                config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, Tokens, Target, new NullUISoundSink(), new UnityFrameClock());
                var root = new DialogTestContext.RootView(); UI.Register(root); UI.Navigator.SetRoot(root);
                Presenter = new MerchantPresenter(UI, Model, market.Inventory, market.Wallet);
                View = new MerchantView(Model, Presenter); UI.Register(View); Presenter.Bind();
            }
            internal void Click(string name) => DialogTestContext.Activate(View.Root.Q<Button>(name));
            public void Dispose() { Presenter.Dispose(); UI.Dispose(); Object.Destroy(config); Panel.Dispose(); }
        }
        [SetUp] public void Setup() => EventHub.Reset();
        [TearDown] public void Cleanup() => EventHub.Reset();

        [UnityTest] public IEnumerator TenTabs_4x3_NoOverflow()
        {
            using var market = new Market(); using var window = new Window(market, 1663, 1247);
            window.Presenter.Show(market.Open(), null);
            // The layout stress case is deliberately larger than the current eight-category content enum.
            window.Model.Categories.Clear();
            for (int i = 0; i < 10; i++) window.Model.Categories.Add("Outfitting category " + i + " supplies");
            window.Model.NotifyCategories();
            yield return null;
            var tabs = window.View.Root.Q<TabBar>("merchant-tabs"); Assert.AreEqual(10, tabs.TabCount);
            var scroll = tabs.Q<ScrollView>(); Assert.Greater(scroll.horizontalScroller.highValue, 0);
            scroll.ScrollTo(tabs.Q<Button>("merchant-tab-9")); yield return null;
            Assert.IsTrue(scroll.contentViewport.worldBound.Contains(tabs.Q<Button>("merchant-tab-9").worldBound.center), "The last category is reachable inside the tabs viewport.");
            var issues = new List<LayoutIssue>(); UILayoutAudit.Run(window.View.Root, window.Panel.Viewport, issues);
            Assert.IsEmpty(issues, string.Join(", ", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
        }
        [UnityTest] public IEnumerator BuyOne_StockDecrementsByOne()
        {
            using var market = new Market();
            Assert.IsTrue(market.Inventory.TryAdd(market.Items[0], 1, AcquireSource.NewGame, out _));
            using var window = new Window(market); var session = market.Open(); window.Presenter.Show(session, null);
            yield return null;
            window.Presenter.SelectCategory(window.Model.Categories.IndexOf("Module"));
            window.View.Root.Q<ListView>("merchant-stock-list").SetSelection(0);
            window.View.Root.Q<SliderInt>("merchant-quantity").value = 1;
            int price = session.BuyPriceOf(1); long credits = market.Wallet.Credits;
            Assert.IsTrue(window.Model.CanBuy); window.Click("merchant-buy");
            Assert.AreEqual(4, session.Stock[1].Count); Assert.AreEqual(5, session.Stock[0].Count, "The filtered row routes to the original stock index.");
            Assert.AreEqual(1, market.Inventory.CountOf(market.Items[1].Id)); Assert.AreEqual(credits - price, market.Wallet.Credits);
            Assert.AreEqual(4, window.Model.Stock[0].Quantity);
            // Visible cargo index zero is real slot one; sell through the same public screen controls.
            window.View.Root.Q<ListView>("merchant-cargo-list").SetSelection(0);
            Assert.IsTrue(window.Model.CanSell); window.Click("merchant-sell");
            Assert.AreEqual(0, market.Inventory.CountOf(market.Items[1].Id)); Assert.AreEqual(1, market.Inventory.CountOf(market.Items[0].Id));
            Assert.AreEqual(5, session.Stock[1].Count);
            Assert.AreEqual(credits - price + session.SellPriceOf(market.Items[1]), market.Wallet.Credits);
        }
        [UnityTest] public IEnumerator BuyFails_ReasonShown_WalletUnchanged()
        {
            using var market = new Market(); market.Wallet.Set(0);
            using var window = new Window(market); var session = market.Open(); window.Presenter.Show(session, null);
            window.Presenter.SelectStock(0); window.Presenter.Buy();
            yield return null;
            Assert.AreEqual(0, market.Wallet.Credits); Assert.AreEqual(5, session.Stock[0].Count); Assert.AreEqual(0, market.Inventory.UsedSlots);
            Assert.IsFalse(window.View.Root.Q<Button>("merchant-buy").enabledSelf);
            Assert.AreEqual("Not enough credits: 100 needed", window.View.Root.Q<Label>("merchant-buy-blocked").text);
            window.UI.Navigator.Pop(window.View);
            int closed = 0;
            var refused = market.Open(true);
            var modal = new ClosingModal(() => window.Presenter.Show(refused,
                () => { Assert.IsNull(market.Service.Current, "Close the owner before its callback."); closed++; }));
            window.UI.Register(modal); window.UI.Navigator.Push(modal); window.UI.Navigator.Pop(modal);
            Assert.IsTrue(window.View.IsOpen, "Show from a pop callback survives its deferred navigation push.");
            Assert.AreSame(refused, market.Service.Current);
            window.Presenter.SelectStock(0); window.Presenter.Buy();
            Assert.IsFalse(window.View.Root.Q<Button>("merchant-buy").enabledSelf); Assert.IsFalse(window.View.Root.Q<Button>("merchant-sell").enabledSelf);
            Assert.AreEqual(refused.RefusalReason, window.View.Root.Q<Label>("merchant-buy-blocked").text);
            Assert.AreEqual(refused.RefusalReason, window.View.Root.Q<Label>("merchant-sell-blocked").text);
            Assert.AreEqual(1, window.Tokens.ActiveTokenCount);
            window.UI.HandleBackPressed(); window.Presenter.ResetSession();
            Assert.AreEqual(1, closed); Assert.IsNull(market.Service.Current); Assert.IsFalse(window.View.IsOpen);
            Assert.AreEqual(0, window.Tokens.ActiveTokenCount); Assert.AreEqual(InputContext.Flight, window.Target.Current);
            Assert.AreEqual(0, market.Wallet.Credits); Assert.AreEqual(5, refused.Stock[0].Count);
        }
        [UnityTest] public IEnumerator Layout_AllLogicalSizes_NoIssues()
        {
            using var market = new Market(128, 200); market.Wallet.Set(long.MaxValue);
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var window = new Window(market, size.x, size.y); window.Presenter.Show(market.Open(), null);
                window.Presenter.SelectStock(0);
                yield return null;
                Assert.AreEqual(128, window.Model.Stock.Count); Assert.AreEqual(200, window.Model.Cargo.Count);
                var stock = window.View.Root.Q<ListView>("merchant-stock-list"); var cargo = window.View.Root.Q<ListView>("merchant-cargo-list");
                stock.ScrollToItem(127); cargo.ScrollToItem(199); yield return null;
                var rows = stock.Query<VisualElement>("merchant-row").ToList();
                Assert.Less(rows.Count, 128, "Stock is virtualized.");
                Assert.IsTrue(rows.Any(row => row.parent.dataSource is MerchantRowViewModel item && item.ItemId == window.Model.Stock[127].ItemId));
                Assert.IsTrue(cargo.Query<VisualElement>("merchant-row").ToList().Any(row => row.parent.dataSource is MerchantRowViewModel item && item.ItemId == window.Model.Cargo[199].ItemId));
                UILayoutAudit.Run(window.View.Root, window.Panel.Viewport, issues);
                Assert.IsEmpty(issues, size + ": " + string.Join(", ", issues.Select(issue => issue.Kind + " " + issue.ElementPath)));
            }
        }
    }
}
