using System;
using System.Diagnostics;
using Game.Core;
using Game.Core.Save;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine.TestTools;

namespace Game.Tests.EditMode.Economy
{
    public sealed class InventorySaveTests
    {
        sealed class Fixture
        {
            public readonly ContentCatalog Catalog;
            public readonly EconomyService Economy;
            public readonly InventorySaveParticipant Save;
            public readonly MerchantDefinition Merchant;
            public readonly ItemDefinition Item;
            public Fixture()
            {
                Item = TestContentFactory.Item("resource_save", so =>
                {
                    TestContentFactory.Set(so, "maxStack", 10);
                    var aliases = so.FindProperty("formerIds"); aliases.arraySize = 1;
                    aliases.GetArrayElementAtIndex(0).stringValue = "resource_old";
                });
                Merchant = TestContentFactory.Create<MerchantDefinition>("merchant_save", so =>
                {
                    var aliases = so.FindProperty("formerIds"); aliases.arraySize = 1;
                    aliases.GetArrayElementAtIndex(0).stringValue = "merchant_old";
                });
                var config = TestContentFactory.Create<EconomyConfigDefinition>("config_economy_save");
                var report = new ValidationReport("InventorySaveTests");
                Catalog = ContentCatalog.Build(new ContentDefinition[] { Item, Merchant, config,
                    TestContentFactory.Create<BlockDefinition>("block_missing"),
                    TestContentFactory.Create<ItemDefinition>("item_missing") }, report);
                Assert.IsFalse(report.HasErrors, report.ToText());
                Economy = new EconomyService(Catalog, config, () => 1, () => 42);
                Economy.Inventory.SetCapacity(20); Save = new InventorySaveParticipant(Economy, Catalog);
            }
            public byte[] Capture() { using var w = new SaveWriter(); Save.Capture(w); return w.ToArray(); }
            public byte[] Payload(int slots = 200, int merchants = 2, bool alias = false)
            {
                using var w = new SaveWriter(); w.WriteInt64(12345); w.WriteVarUInt32((uint)slots);
                for (int i = 0; i < slots; i++)
                {
                    w.WriteUInt16((ushort)i); w.WriteContentId(i == slots - 1 ? new ContentId("item_removed") : Item.Id); w.WriteInt32(1);
                }
                w.WriteVarUInt32((uint)merchants);
                for (int i = 0; i < merchants; i++)
                {
                    w.WriteContentId(i == 0 ? (alias ? new ContentId("merchant_old") : Merchant.Id) : new ContentId("merchant_unknown_" + i));
                    w.WriteDouble(12.5 + i); w.WriteVarUInt32(2);
                    w.WriteContentId(i == 0 ? Item.Id : new ContentId("resource_old")); w.WriteInt32(37); w.WriteSingle(1.25f); w.WriteBool(false);
                    w.WriteContentId(new ContentId("item_removed")); w.WriteInt32(-1); w.WriteSingle(-0f); w.WriteBool(true);
                }
                return w.ToArray();
            }
        }
        [TearDown] public void TearDown() { TestContentFactory.DestroyAll(); LogAssert.NoUnexpectedReceived(); }
        [Test] public void RoundTrip_ByteIdentical()
        {
            var f = new Fixture(); byte[] before = f.Payload(); f.Save.Restore(new SaveReader(before), 1);
            CollectionAssert.AreEqual(before, f.Capture());
            Assert.AreEqual(200, f.Economy.Inventory.UsedSlots); Assert.AreEqual(20, f.Economy.Inventory.Capacity);
            Assert.AreEqual(12345, f.Economy.Wallet.Credits);
            Assert.AreEqual(199, f.Economy.Inventory.CountOf(f.Item.Id));
            Assert.AreEqual(1, f.Economy.Inventory.Get(199).Count);
            var session = f.Economy.Merchants.OpenSession(f.Merchant, 0, false, "");
            Assert.AreEqual(37, session.Stock[0].Count); Assert.IsTrue(session.Stock[1].Buyback);
        }
        [Test] public void MissingItem_Preserved()
        {
            var f = new Fixture(); var bytes = f.Payload(1, 1); f.Save.Restore(new SaveReader(bytes), 1);
            var slot = f.Economy.Inventory.Get(0);
            Assert.AreSame(f.Catalog.MissingItem, slot.Item); Assert.AreEqual(new ContentId("item_removed"), slot.OriginalId);
            var session = f.Economy.Merchants.OpenSession(f.Merchant, 0, false, "");
            var stock = session.Stock[1];
            Assert.IsFalse(session.TryBuy(1, 1, out var reason)); Assert.AreEqual("Item unavailable", reason);
            Assert.AreSame(f.Catalog.MissingItem, stock.Item); Assert.AreEqual(slot.OriginalId, stock.OriginalId);
            Assert.AreEqual(-1, stock.Count); CollectionAssert.AreEqual(bytes, f.Capture());
        }
        [Test] public void UnknownMerchant_Preserved()
        {
            var f = new Fixture(); var bytes = f.Payload(0); f.Save.Restore(new SaveReader(bytes), 1);
            Assert.IsNull(f.Economy.Merchants.Current);
            CollectionAssert.AreEqual(bytes, f.Capture());
            f.Economy.Merchants.OpenSession(f.Merchant, 0, false, "").Close();
            CollectionAssert.AreEqual(bytes, f.Capture());
            f.Save.Restore(new SaveReader(f.Payload(0, 2, alias: true)), 1);
            var session = f.Economy.Merchants.OpenSession(f.Merchant, 0, false, "");
            Assert.AreEqual(37, session.Stock[0].Count, "A renamed merchant must reuse restored stock");
            CollectionAssert.AreEqual(bytes, f.Capture(), "Known aliases resave canonically; unknown merchants remain unchanged");
        }
        [Test] public void Restore_Replaces()
        {
            var f = new Fixture(); f.Save.Restore(new SaveReader(f.Payload()), 1);
            var old = f.Economy.Merchants.OpenSession(f.Merchant, 0, false, "");
            byte[] replacement = f.Payload(1, 0); f.Save.Restore(new SaveReader(replacement), 1);
            Assert.IsNull(f.Economy.Merchants.Current); Assert.IsFalse(old.CanBuy(0, 1, out _));
            Assert.AreEqual(1, f.Economy.Inventory.UsedSlots); Assert.IsTrue(f.Economy.Inventory.Get(199).IsEmpty);
            Assert.AreEqual(0, f.Economy.Inventory.CountOf(f.Item.Id)); CollectionAssert.AreEqual(replacement, f.Capture());
            f.Save.Restore(SaveReader.Empty, 0);
            Assert.AreEqual(0, f.Economy.Inventory.UsedSlots); Assert.AreEqual(0, f.Economy.Wallet.Credits);
            Assert.AreEqual(20, f.Economy.Inventory.Capacity);
        }
        [Test, Performance, Category("Performance")] public void Capture_Time()
        {
            var f = new Fixture(); f.Save.Restore(new SaveReader(f.Payload(200, 5)), 1);
            using var writer = new SaveWriter(); f.Save.Capture(writer);
            var samples = new double[10];
            for (int i = 0; i < samples.Length; i++)
            {
                writer.Reset(); long start = Stopwatch.GetTimestamp(); f.Save.Capture(writer);
                samples[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            PerfRun.Gate("InventoryCapture200", samples, .2);
        }
    }
}
