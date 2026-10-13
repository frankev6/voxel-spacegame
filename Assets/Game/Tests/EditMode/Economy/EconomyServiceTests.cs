using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Core.Flow;
using Game.Data.Definitions;
using Game.Data.Logic;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static Game.Tests.EditMode.Economy.LootRollerTests;

namespace Game.Tests.EditMode.Economy
{
    /// <summary>SPEC-50 ECO-004: four facade/lifecycle checks using production inventory, wallet, merchants and loot rolling.</summary>
    public sealed class EconomyServiceTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly ItemDefinition[] Items;
            public readonly LootTableDefinition Loot;
            public readonly MerchantDefinition Merchant;
            public readonly ContentCatalog Catalog;
            public readonly EconomyService Service;
            public readonly List<ItemAcquired> Acquired = new List<ItemAcquired>();
            public ulong Seed = 123;
            public double Now = 42;
            public ILogicEconomy Logic => Service;
            public Fixture()
            {
                Items = new[] { Item("resource_service_a"), Item("resource_service_b"), Item("resource_service_c") };
                Loot = Table("loot_service", 3, Entry(Items[0], 25, 1, 4), Entry(Items[1], 35, 2, 5), Entry(Items[2], 40, 1, 2));
                Merchant = TestContentFactory.Create<MerchantDefinition>("merchant_service", so =>
                {
                    TestContentFactory.Set(so, "displayName", "Test Merchant"); so.FindProperty("stock").arraySize = 1;
                    TestContentFactory.Set(so, "stock.Array.data[0].item", Items[0]); TestContentFactory.Set(so, "stock.Array.data[0].count", 5);
                });
                var config = TestContentFactory.Create<EconomyConfigDefinition>("config_service", so =>
                {
                    so.FindProperty("startCredits").longValue = 1234;
                    Entries(so, "startItems", Entry(Items[0], 1, 3, 9), Entry(Items[1], 100, 2, 4));
                });
                var report = new ValidationReport("EconomyServiceTests");
                Catalog = ContentCatalog.Build(new ContentDefinition[]
                { TestContentFactory.Create<BlockDefinition>("block_missing"), TestContentFactory.Item("item_missing"), Items[0], Items[1], Items[2], Loot, Merchant, config }, report);
                Assert.AreEqual(0, report.ErrorCount);
                Service = new EconomyService(Catalog, config, () => Seed, () => Now);
                EventBus<ItemAcquired>.Drain(); EventBus<ItemAcquired>.Subscribe(Acquired.Add);
            }
            static ItemDefinition Item(string id) => TestContentFactory.Item(id, so =>
            { TestContentFactory.Set(so, "category", (int)ItemCategory.Resource); TestContentFactory.Set(so, "maxStack", 10); TestContentFactory.Set(so, "basePrice", 10); });
            public int Index(int item) => Catalog.IndexOf(Items[item]);
            public void Drain() => EventBus<ItemAcquired>.Drain();
            public void Dispose() { Drain(); EventBus<ItemAcquired>.Unsubscribe(Acquired.Add); }
        }
        [SetUp] public void SetUp() => GameLog.ResetOnceKeys();
        [TearDown] public void TearDown() { TestContentFactory.DestroyAll(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void NewGame_Start()
        {
            using var f = new Fixture(); f.Logic.GiveCredits(100); f.Logic.GiveItems(f.Index(0), 2);
            var oldSession = f.Service.Merchants.OpenSession(f.Merchant, 0, false, null); Assert.IsTrue(oldSession.TryBuy(0, 1, out _));
            f.Drain(); f.Acquired.Clear(); f.Service.Inventory.SetCapacity(2);
            var inventory = f.Service.Inventory; var wallet = f.Service.Wallet; var merchants = f.Service.Merchants;
            int creditChanges = 0; Action changed = () => creditChanges++; f.Logic.CreditsChanged += changed;
            INewGameInitializer initializer = f.Service; Assert.AreEqual(50, initializer.NewGameOrder);
            initializer.InitializeNewGame(new NewGameArgs("Loot Test", 999, false, ContentId.None)); f.Drain();
            Assert.AreEqual(1234, f.Logic.GetCredits()); Assert.AreEqual(3, f.Logic.GetItemCount(f.Index(0))); Assert.AreEqual(2, f.Logic.GetItemCount(f.Index(1)));
            Assert.AreEqual(2, inventory.Capacity); Assert.IsEmpty(f.Acquired); Assert.IsNull(merchants.Current);
            Assert.IsFalse(oldSession.TryBuy(0, 1, out var reason)); Assert.AreEqual("Session closed", reason);
            Assert.AreEqual(5, merchants.OpenSession(f.Merchant, 0, false, null).Stock[0].Count);
            ISessionModel session = f.Service; session.ResetToEmpty(); f.Drain();
            Assert.AreEqual(0, f.Logic.GetCredits()); Assert.AreEqual(0, inventory.UsedSlots); Assert.AreEqual(2, inventory.Capacity);
            Assert.IsNull(merchants.Current); Assert.IsEmpty(f.Acquired); Assert.AreEqual(2, creditChanges);
            Assert.AreSame(inventory, f.Service.Inventory); Assert.AreSame(wallet, f.Service.Wallet); Assert.AreSame(merchants, f.Service.Merchants);
            f.Logic.CreditsChanged -= changed;
        }
        [Test] public void Give_PublishesAcquired()
        {
            using var f = new Fixture(); f.Service.Inventory.SetCapacity(1);
            var observed = new List<(int item, int count)>(); Action<int> countChanged = index => observed.Add((index, f.Logic.GetItemCount(index)));
            int creditsChanged = 0; Action creditChanged = () => creditsChanged++;
            f.Logic.ItemCountChanged += countChanged; f.Logic.CreditsChanged += creditChanged;
            f.Logic.GiveItems(f.Index(0), 4); f.Logic.GiveItems(f.Index(0), 0); f.Logic.GiveItems(f.Index(1), 2); f.Drain();
            Assert.AreEqual(4, f.Logic.GetItemCount(f.Index(0))); Assert.AreEqual(2, f.Logic.GetItemCount(f.Index(1)));
            Assert.AreEqual(f.Items[1], f.Service.Inventory.Get(1).Item, "Narrative gives use overflow after cargo fills.");
            Assert.AreEqual(2, f.Acquired.Count); Assert.AreEqual((f.Index(0), 4), observed[0]); Assert.AreEqual((f.Index(1), 2), observed[1]);
            for (int i = 0; i < f.Acquired.Count; i++)
            { Assert.AreEqual(AcquireSource.Narrative, f.Acquired[i].Source); Assert.AreEqual(f.Index(i), f.Acquired[i].ItemIndex); Assert.AreEqual(f.Items[i].Id.Hash, f.Acquired[i].Item); }
            Assert.AreEqual(4, f.Acquired[0].Count); Assert.AreEqual(2, f.Acquired[1].Count);
            int invalid = f.Index(0) + 65536;
            LogAssert.Expect(LogType.Error, $"[Economy] Unknown item index {invalid}.");
            f.Logic.GiveItems(invalid, 1); f.Logic.GiveItems(invalid, 1); Assert.AreEqual(0, f.Logic.GetItemCount(invalid));
            Assert.AreEqual(4, f.Logic.GetItemCount(f.Index(0)));
            f.Logic.GiveCredits(5); Assert.IsTrue(f.Logic.TryTakeCredits(3)); Assert.AreEqual(2, f.Logic.GetCredits()); Assert.AreEqual(2, creditsChanged);
            f.Logic.ItemCountChanged -= countChanged; f.Logic.CreditsChanged -= creditChanged;
            f.Logic.GiveItems(f.Index(0), 1); f.Logic.GiveCredits(4); Assert.AreEqual(2, observed.Count); Assert.AreEqual(2, creditsChanged);
        }
        [Test] public void TakeItems_AllOrNothing()
        {
            using var f = new Fixture(); f.Logic.GiveItems(f.Index(0), 7); f.Logic.GiveItems(f.Index(1), 3); f.Drain(); f.Acquired.Clear();
            int changes = 0; Action<int> changed = _ => changes++; f.Logic.ItemCountChanged += changed;
            Assert.IsFalse(f.Logic.TryTakeItems(f.Index(0), 8)); Assert.IsFalse(f.Logic.TryTakeItems(f.Index(0), 0));
            Assert.IsFalse(f.Logic.TryTakeItems(-1, 1)); Assert.IsFalse(f.Logic.TryTakeItems(f.Index(0) + 65536, 1));
            Assert.AreEqual(7, f.Logic.GetItemCount(f.Index(0))); Assert.AreEqual(3, f.Logic.GetItemCount(f.Index(1))); Assert.AreEqual(0, changes);
            Assert.IsTrue(f.Logic.TryTakeItems(f.Index(0), 3)); Assert.AreEqual(4, f.Logic.GetItemCount(f.Index(0)));
            Assert.AreEqual(3, f.Logic.GetItemCount(f.Index(1))); Assert.AreEqual(1, changes); f.Drain(); Assert.IsEmpty(f.Acquired);
            f.Logic.GiveCredits(100); Assert.IsFalse(f.Logic.TryTakeCredits(101)); Assert.AreEqual(100, f.Logic.GetCredits());
            Assert.IsTrue(f.Logic.TryTakeCredits(100)); Assert.AreEqual(0, f.Logic.GetCredits());
            Assert.Throws<ArgumentOutOfRangeException>(() => f.Logic.GiveCredits(-1)); Assert.Throws<ArgumentOutOfRangeException>(() => f.Logic.TryTakeCredits(-1));
            f.Logic.ItemCountChanged -= changed;
        }
        [Test] public void Loot_UsesCampaignSeed()
        {
            using var f = new Fixture(); var expected = new List<(ItemDefinition item, int count)>(); int tableIndex = f.Catalog.IndexOf(f.Loot);
            foreach (var input in new[] { (seed: 123UL, salt: 456UL, picks: -1), (seed: 987654UL, salt: 456UL, picks: 6), (seed: 987654UL, salt: 987UL, picks: 6) })
            {
                f.Seed = input.seed; f.Service.ResetToEmpty(); f.Drain(); f.Acquired.Clear();
                LootRoller.Roll(f.Loot, input.picks, StableHash.Combine(input.seed, input.salt), expected);
                for (int grant = 1; grant <= 2; grant++)
                {
                    f.Acquired.Clear(); f.Logic.GiveLoot(tableIndex, input.picks, input.salt); f.Drain();
                    foreach (var item in f.Items) Assert.AreEqual(Count(expected, item) * grant, f.Service.Inventory.CountOf(item.Id));
                    Assert.AreEqual(expected.Count, f.Acquired.Count);
                    for (int i = 0; i < expected.Count; i++)
                    { Assert.AreEqual(AcquireSource.Loot, f.Acquired[i].Source); Assert.AreEqual(expected[i].item.Id.Hash, f.Acquired[i].Item); Assert.AreEqual(expected[i].count, f.Acquired[i].Count); }
                }
            }
            LogAssert.Expect(LogType.Error, "[Economy] Unknown loot table index 65537.");
            var before = new int[f.Items.Length]; for (int i = 0; i < before.Length; i++) before[i] = f.Service.Inventory.CountOf(f.Items[i].Id);
            f.Acquired.Clear(); f.Logic.GiveLoot(65537, 1, 1); f.Logic.GiveLoot(65537, 1, 1); f.Drain(); Assert.IsEmpty(f.Acquired);
            for (int i = 0; i < before.Length; i++) Assert.AreEqual(before[i], f.Service.Inventory.CountOf(f.Items[i].Id));
        }
    }
}
