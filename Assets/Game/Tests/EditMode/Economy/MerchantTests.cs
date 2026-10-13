using System;
using System.Collections.Generic;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Economy
{
    /// <summary>SPEC-50 ECO-005: the eight required managed merchant checks, using real inventory and wallet models.</summary>
    public sealed class MerchantTests
    {
        sealed class Fixture
        {
            public readonly ContentCatalog Catalog;
            public readonly InventoryModel Inventory;
            public readonly Wallet Wallet = new Wallet();
            public readonly MerchantService Service;
            public readonly MerchantDefinition Merchant, Other;
            public readonly ItemDefinition StockItem, Module, Quest, Untradeable, Consumable, Valueless;
            public double Now;
            public Fixture(int stockCount = 5, float restockHours = 24, int basePrice = 100)
            {
                StockItem = Item("resource_test", ItemCategory.Resource, basePrice);
                Module = Item("module_test", ItemCategory.Module);
                Quest = Item("item_quest_test", ItemCategory.Quest);
                Untradeable = Item("resource_untradeable", ItemCategory.Resource, tradeable: false);
                Consumable = Item("item_consumable_test", ItemCategory.Consumable);
                Valueless = Item("resource_valueless", ItemCategory.Resource, 0);
                Merchant = Definition("merchant_test", stockCount, restockHours); Other = Definition("merchant_other", 7, restockHours);
                var config = TestContentFactory.Create<EconomyConfigDefinition>("config_economy_test");
                var report = new ValidationReport("MerchantTests");
                Catalog = ContentCatalog.Build(new ContentDefinition[]
                {
                    TestContentFactory.Create<BlockDefinition>("block_missing"), TestContentFactory.Create<ItemDefinition>("item_missing"),
                    StockItem, Module, Quest, Untradeable, Consumable, Valueless, Merchant, Other, config
                }, report);
                Assert.AreEqual(0, report.ErrorCount);
                Inventory = new InventoryModel(Catalog); Wallet.Set(1000);
                Service = new MerchantService(Inventory, Wallet, Catalog, config, () => Now);
            }
            static ItemDefinition Item(string id, ItemCategory category, int price = 100, bool tradeable = true)
                => TestContentFactory.Item(id, so =>
                {
                    TestContentFactory.Set(so, "category", (int)category); TestContentFactory.Set(so, "basePrice", price);
                    TestContentFactory.Set(so, "maxStack", 10); TestContentFactory.Set(so, "tradeable", tradeable);
                });
            MerchantDefinition Definition(string id, int count, float hours)
                => TestContentFactory.Create<MerchantDefinition>(id, so =>
                {
                    TestContentFactory.Set(so, "displayName", "Outfitter"); TestContentFactory.Set(so, "restockHours", hours);
                    so.FindProperty("stock").arraySize = 1;
                    TestContentFactory.Set(so, "stock.Array.data[0].item", StockItem);
                    TestContentFactory.Set(so, "stock.Array.data[0].count", count);
                    TestContentFactory.Set(so, "stock.Array.data[0].priceFactor", 0f);
                });
            public MerchantSession Open(int relation = 0, bool refuse = false, string reason = null)
                => Service.OpenSession(Merchant, relation, refuse, reason);
            public void Add(ItemDefinition item, int count) => Assert.IsTrue(Inventory.TryAdd(item, count, AcquireSource.EditorRefund, out _));
            public int Slot(ItemDefinition item)
            { for (int i = 0; i < InventoryModel.MaxSlots; i++) if (Inventory.Get(i).Item == item) return i; return -1; }
            public void Unchanged(MerchantSession session, Action rejected)
            {
                long credits = Wallet.Credits; int used = Inventory.UsedSlots;
                var cargo = new ItemStack[InventoryModel.MaxSlots];
                for (int i = 0; i < cargo.Length; i++) cargo[i] = Inventory.Get(i);
                var stock = new MerchantStockState[session.Stock.Count];
                for (int i = 0; i < stock.Length; i++)
                {
                    var row = session.Stock[i]; stock[i] = new MerchantStockState
                    { Item = row.Item, Count = row.Count, PriceFactor = row.PriceFactor, Buyback = row.Buyback, OriginalId = row.OriginalId };
                }
                int changed = 0; Action onChange = () => changed++; session.Changed += onChange;
                try
                {
                    rejected(); Assert.AreEqual(0, changed); Assert.AreEqual(credits, Wallet.Credits); Assert.AreEqual(used, Inventory.UsedSlots);
                    Assert.AreEqual(stock.Length, session.Stock.Count);
                    for (int i = 0; i < stock.Length; i++)
                    {
                        Assert.AreEqual(stock[i].Item, session.Stock[i].Item); Assert.AreEqual(stock[i].Count, session.Stock[i].Count);
                        Assert.AreEqual(stock[i].PriceFactor, session.Stock[i].PriceFactor); Assert.AreEqual(stock[i].Buyback, session.Stock[i].Buyback);
                        Assert.AreEqual(stock[i].OriginalId, session.Stock[i].OriginalId);
                    }
                    for (int i = 0; i < cargo.Length; i++) Assert.AreEqual(cargo[i], Inventory.Get(i), $"slot {i}");
                }
                finally { session.Changed -= onChange; }
            }
            public void RejectBuy(MerchantSession session, int index, int count, string expected)
                => Unchanged(session, () =>
                {
                    Assert.IsFalse(session.CanBuy(index, count, out string reason)); Assert.AreEqual(expected, reason);
                    Assert.IsFalse(session.TryBuy(index, count, out reason)); Assert.AreEqual(expected, reason);
                });
            public void RejectSell(MerchantSession session, int slot, int count, string expected)
                => Unchanged(session, () =>
                {
                    Assert.IsFalse(session.CanSell(slot, count, out string reason)); Assert.AreEqual(expected, reason);
                    Assert.IsFalse(session.TrySell(slot, count, out reason)); Assert.AreEqual(expected, reason);
                });
        }
        [TearDown] public void TearDown() { EventBus<ItemAcquired>.Drain(); TestContentFactory.DestroyAll(); LogAssert.NoUnexpectedReceived(); }

        [Test] public void BuyOne_StockDecrements()
        {
            var f = new Fixture(); var session = f.Open(); int changed = 0;
            session.Changed += () =>
            {
                changed++; Assert.AreEqual(4, session.Stock[0].Count); Assert.AreEqual(900, f.Wallet.Credits);
                Assert.AreEqual(1, f.Inventory.CountOf(f.StockItem.Id));
                Assert.IsTrue(session.CanBuy(0, 1, out _));
                Assert.IsFalse(session.TryBuy(0, 1, out var reason)); Assert.AreEqual("Trade in progress", reason);
            };
            var acquired = new List<ItemAcquired>(); Action<ItemAcquired> receive = acquired.Add;
            EventBus<ItemAcquired>.Drain(); EventBus<ItemAcquired>.Subscribe(receive);
            try
            {
                Assert.IsTrue(session.CanBuy(0, 1, out var reason)); Assert.IsNull(reason);
                Assert.IsTrue(session.TryBuy(0, 1, out reason)); Assert.IsNull(reason); EventBus<ItemAcquired>.Drain();
                Assert.AreEqual(1, changed); Assert.AreEqual(4, session.Stock[0].Count); Assert.AreEqual(900, f.Wallet.Credits);
                Assert.AreEqual(1, f.Inventory.CountOf(f.StockItem.Id)); Assert.AreEqual(1, acquired.Count);
                Assert.AreEqual(AcquireSource.Purchase, acquired[0].Source); Assert.AreEqual(1, acquired[0].Count);
                Assert.AreEqual(f.StockItem.Id.Hash, acquired[0].Item); Assert.AreEqual(f.Catalog.IndexOf(f.StockItem), acquired[0].ItemIndex);
            }
            finally { EventBus<ItemAcquired>.Unsubscribe(receive); }
        }
        [Test] public void Buy_Failures_NoChange()
        {
            var f = new Fixture(); var session = f.Open();
            f.RejectBuy(session, -1, 0, "Not in stock"); f.RejectBuy(session, 1, 1, "Not in stock");
            f.RejectBuy(session, 0, 0, "Invalid quantity"); f.RejectBuy(session, 0, 6, "Only 5 in stock");
            f.Wallet.Set(99); f.RejectBuy(session, 0, 1, "Not enough credits: 100 needed");
            f.Inventory.SetCapacity(1); f.Add(f.Module, 10); f.Wallet.Set(100);
            f.RejectBuy(session, 0, 1, "Cargo full"); Assert.AreEqual(0, f.Inventory.CountOf(f.StockItem.Id));
            var large = new Fixture(stockCount: -1, basePrice: int.MaxValue); var unlimited = large.Open(); large.Wallet.Set(Wallet.MaxCredits);
            long total = (long)int.MaxValue * int.MaxValue;
            large.RejectBuy(unlimited, 0, int.MaxValue, $"Not enough credits: {total} needed");
        }
        [Test] public void Unlimited_Stays()
        {
            var f = new Fixture(stockCount: -1); var session = f.Open();
            Assert.IsTrue(session.TryBuy(0, 2, out _)); Assert.AreEqual(-1, session.Stock[0].Count);
            Assert.AreEqual(800, f.Wallet.Credits); Assert.AreEqual(2, f.Inventory.CountOf(f.StockItem.Id));
            Assert.IsTrue(session.TrySell(f.Slot(f.StockItem), 1, out _)); Assert.AreEqual(-1, session.Stock[0].Count);
            Assert.AreEqual(850, f.Wallet.Credits); Assert.AreEqual(1, session.Stock.Count);
            f.Now = 86400; Assert.AreEqual(-1, f.Open().Stock[0].Count);
        }
        [Test] public void Sell_Buyback_RemovedAtRestock()
        {
            var f = new Fixture(); var session = f.Open(); Assert.IsTrue(session.TryBuy(0, 1, out _));
            f.Add(f.Module, 3); int slot = f.Slot(f.Module);
            Assert.IsTrue(session.TrySell(slot, 2, out _)); Assert.AreEqual(1000, f.Wallet.Credits);
            Assert.AreEqual(1, f.Inventory.Get(slot).Count); Assert.AreEqual(2, session.Stock.Count);
            Assert.IsTrue(session.Stock[1].Buyback); Assert.AreEqual(f.Module, session.Stock[1].Item);
            Assert.AreEqual(2, session.Stock[1].Count); Assert.AreEqual(1, session.Stock[1].PriceFactor);
            Assert.IsTrue(session.TrySell(slot, 1, out _)); Assert.AreEqual(3, session.Stock[1].Count); Assert.AreEqual(1050, f.Wallet.Credits);
            Assert.IsTrue(session.TryBuy(1, 1, out _)); Assert.AreEqual(2, session.Stock[1].Count); Assert.AreEqual(950, f.Wallet.Credits);
            f.Now = 86400; session = f.Open(); Assert.AreEqual(1, session.Stock.Count); Assert.AreEqual(5, session.Stock[0].Count);
            Assert.IsFalse(session.Stock[0].Buyback);
            Assert.IsTrue(session.TrySell(f.Slot(f.Module), 1, out _)); Assert.AreEqual(2, session.Stock.Count);
            Assert.IsTrue(session.TryBuy(1, 1, out _)); Assert.AreEqual(1, session.Stock.Count, "An exhausted buyback row disappears immediately.");
            Assert.IsTrue(session.TryBuy(0, 5, out _)); Assert.AreEqual(0, session.Stock[0].Count);
            Assert.AreEqual(1, session.Stock.Count, "An exhausted authored row remains for restocking.");
            Assert.IsTrue(session.TrySell(f.Slot(f.StockItem), 1, out _)); Assert.AreEqual(1, session.Stock[0].Count);
        }
        [Test] public void Sell_Refusals()
        {
            var f = new Fixture(); var session = f.Open();
            f.Add(f.Quest, 1); f.Add(f.Untradeable, 1); f.Add(f.Consumable, 1); f.Add(f.Valueless, 1); f.Add(f.Module, 1);
            f.RejectSell(session, 199, 1, "Nothing to sell"); f.RejectSell(session, -1, 1, "Nothing to sell");
            f.RejectSell(session, f.Slot(f.Quest), 0, "Cannot sell this item");
            f.RejectSell(session, f.Slot(f.Untradeable), 1, "Cannot sell this item");
            f.RejectSell(session, f.Slot(f.Consumable), 1, "Outfitter does not buy Consumable");
            f.RejectSell(session, f.Slot(f.Module), 0, "Invalid quantity"); f.RejectSell(session, f.Slot(f.Module), 2, "Invalid quantity");
            f.RejectSell(session, f.Slot(f.Valueless), 1, "No value");
            Assert.AreEqual(0, session.SellPriceOf(f.Quest)); Assert.AreEqual(0, session.SellPriceOf(f.Consumable));
            var full = new Fixture(stockCount: int.MaxValue); var fullSession = full.Open(); full.Add(full.StockItem, 1);
            full.RejectSell(fullSession, full.Slot(full.StockItem), 1, "Stock full");
        }
        [Test] public void Prices_Relation()
        {
            Assert.AreEqual(90, PriceMath.BuyPrice(100, 0, 1, 50, .002f)); Assert.AreEqual(110, PriceMath.BuyPrice(100, 1, 1, -50, .002f));
            Assert.AreEqual(90, PriceMath.BuyPrice(100, 1, 1, int.MaxValue, .002f));
            Assert.AreEqual(110, PriceMath.BuyPrice(100, 1, 1, int.MinValue, .002f));
            Assert.AreEqual(51, PriceMath.BuyPrice(101, .5f, 1, 0, .002f)); Assert.AreEqual(50, PriceMath.SellPrice(101, .5f, 0, .002f));
            Assert.AreEqual(55, PriceMath.SellPrice(100, .5f, 50, .002f)); Assert.AreEqual(45, PriceMath.SellPrice(100, .5f, -50, .002f));
            Assert.AreEqual(1, PriceMath.BuyPrice(0, 1, 1, 0, .002f)); Assert.AreEqual(1, PriceMath.BuyPrice(100, 1, 1, 50, 1));
            Assert.AreEqual(0, PriceMath.SellPrice(100, .5f, -50, 1));
            Assert.AreEqual(int.MaxValue, PriceMath.BuyPrice(int.MaxValue, float.MaxValue, 1, 0, .002f));
            Assert.AreEqual(int.MaxValue, PriceMath.SellPrice(int.MaxValue, 1, 50, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PriceMath.BuyPrice(100, float.NaN, 1, 0, .002f));
            var f = new Fixture(); var session = f.Open(50); Assert.AreEqual(90, session.BuyPriceOf(0)); Assert.AreEqual(55, session.SellPriceOf(f.StockItem));
            Assert.IsTrue(session.TryBuy(0, 1, out _)); Assert.AreEqual(910, f.Wallet.Credits);
            Assert.IsTrue(session.TrySell(f.Slot(f.StockItem), 1, out _)); Assert.AreEqual(965, f.Wallet.Credits);
        }
        [Test] public void Restock_GameTime()
        {
            var f = new Fixture(); var first = f.Open(); Assert.IsTrue(first.TryBuy(0, 2, out _));
            Assert.AreSame(first, f.Service.Current); f.Now = 86399; var before = f.Open(); Assert.AreEqual(3, before.Stock[0].Count);
            f.RejectBuy(first, 0, 1, "Session closed"); f.RejectSell(first, f.Slot(f.StockItem), 1, "Session closed");
            f.Now = 86400; Assert.AreEqual(3, before.Stock[0].Count, "Game time alone does not mutate an open session.");
            var restocked = f.Open(); Assert.AreEqual(5, restocked.Stock[0].Count);
            Assert.IsTrue(restocked.TryBuy(0, 1, out _)); f.Now = 0; Assert.AreEqual(4, f.Open().Stock[0].Count, "A backwards clock does not restock.");
            var other = f.Service.OpenSession(f.Other, 0, false, null); Assert.AreEqual(7, other.Stock[0].Count);
            Assert.IsTrue(other.TryBuy(0, 1, out _)); Assert.AreEqual(4, f.Open().Stock[0].Count, "Stock belongs to the definition, not the session or location.");
            var current = f.Service.Current; current.Close(); current.Close(); Assert.IsNull(f.Service.Current);
            f.Open(); f.Service.ResetToEmpty(); Assert.IsNull(f.Service.Current); Assert.AreEqual(5, f.Open().Stock[0].Count);
            var never = new Fixture(restockHours: 0); Assert.IsTrue(never.Open().TryBuy(0, 1, out _));
            never.Now = 1e12; Assert.AreEqual(4, never.Open().Stock[0].Count);
        }
        [Test] public void Refused_BlocksTrades()
        {
            var f = new Fixture(); f.Add(f.StockItem, 2); var session = f.Open(refuse: true, reason: "Hostile station");
            Assert.IsTrue(session.Refused); Assert.AreEqual("Hostile station", session.RefusalReason);
            f.RejectBuy(session, -1, 0, "Hostile station"); f.RejectBuy(session, 0, 1, "Hostile station");
            f.RejectSell(session, -1, 0, "Hostile station"); f.RejectSell(session, f.Slot(f.StockItem), 1, "Hostile station");
            var allowed = f.Open(reason: "ignored"); Assert.IsFalse(allowed.Refused); Assert.AreEqual("", allowed.RefusalReason);
            Assert.IsTrue(allowed.TryBuy(0, 1, out _));
            var unnamed = f.Open(refuse: true); f.RejectBuy(unnamed, 0, 1, "");
        }
    }
}
