using System;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Economy;
using Game.Tests.EditMode.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Random = Unity.Mathematics.Random;

namespace Game.Tests.EditMode.Economy
{
    public sealed class InventoryModelTests
    {
        internal sealed class Fixture : IDisposable
        {
            public readonly ContentCatalog Catalog;
            public readonly ItemDefinition[] Items;
            public readonly InventoryModel Inventory;
            public Fixture(int itemCount = 3)
            {
                Items = new ItemDefinition[itemCount];
                var definitions = new List<ContentDefinition>
                { TestContentFactory.Create<BlockDefinition>("block_missing"), TestContentFactory.Create<ItemDefinition>("item_missing") };
                for (int i = 0; i < Items.Length; i++)
                {
                    int n = i;
                    Items[i] = TestContentFactory.Create<ItemDefinition>($"item_test_{i:00}", so =>
                    {
                        TestContentFactory.Set(so, "displayName", $"Item {n:00}");
                        TestContentFactory.Set(so, "maxStack", 10);
                        TestContentFactory.Set(so, "category", (int)ItemCategory.Resource);
                    });
                    definitions.Add(Items[i]);
                }
                var report = new ValidationReport("InventoryModelTests"); Catalog = ContentCatalog.Build(definitions, report);
                Assert.AreEqual(0, report.ErrorCount); Inventory = new InventoryModel(Catalog);
            }
            public void Add(int item, int count) => Assert.IsTrue(Inventory.TryAdd(Items[item], count, AcquireSource.EditorRefund, out _));
            public ItemStack[] Snapshot()
            {
                var result = new ItemStack[InventoryModel.MaxSlots];
                for (int i = 0; i < result.Length; i++) result[i] = Inventory.Get(i);
                return result;
            }
            public void SameSlots(ItemStack[] expected)
            {
                for (int i = 0; i < expected.Length; i++)
                {
                    var actual = Inventory.Get(i); Assert.AreEqual(expected[i].Item, actual.Item);
                    Assert.AreEqual(expected[i].Count, actual.Count); Assert.AreEqual(expected[i].OriginalId, actual.OriginalId);
                }
            }
            public void Invariants()
            {
                var counts = new int[Items.Length]; int used = 0;
                for (int i = 0; i < InventoryModel.MaxSlots; i++)
                {
                    var stack = Inventory.Get(i);
                    if (stack.IsEmpty) { Assert.AreEqual(0, stack.Count); continue; }
                    used++; Assert.That(stack.Count, Is.InRange(1, stack.Item.MaxStack));
                    int item = Array.IndexOf(Items, stack.Item); Assert.GreaterOrEqual(item, 0); counts[item] += stack.Count;
                }
                Assert.AreEqual(used, Inventory.UsedSlots);
                for (int i = 0; i < Items.Length; i++)
                {
                    Assert.AreEqual(counts[i], Inventory.CountOf(Items[i].Id));
                    Assert.AreEqual(counts[i], Inventory.CountOf(Catalog.IndexOf<ItemDefinition>(Items[i])));
                }
            }
            public void Dispose() => TestContentFactory.DestroyAll();
        }

        [Test] public void RandomOps_Invariants()
        {
            using var f = new Fixture(); var inv = f.Inventory; var random = new Random(0x5A17u);
            for (int n = 0; n < 10000; n++)
            {
                int operation = random.NextInt(8), item = random.NextInt(f.Items.Length), amount = random.NextInt(1, 21);
                var before = f.Snapshot(); var counts = new int[f.Items.Length];
                for (int i = 0; i < counts.Length; i++) counts[i] = inv.CountOf(f.Items[i].Id);
                bool success = true;
                switch (operation)
                {
                    case 0: success = inv.TryAdd(f.Items[item], amount, AcquireSource.EditorRefund, out _); break;
                    case 1:
                        if (inv.UsedSlots < 190) inv.Give(f.Items[item], amount, AcquireSource.NewGame);
                        else inv.TryTake(f.Items[item], amount);
                        break;
                    case 2: success = inv.TryTake(f.Items[item], amount); break;
                    case 3: success = inv.TryApplyAtomic(new[] { new ItemDelta(f.Items[item].Id, -amount), new ItemDelta(f.Items[(item + 1) % counts.Length].Id, amount) }); break;
                    case 4: success = inv.TryMove(random.NextInt(200), random.NextInt(200)); break;
                    case 5: inv.Sort(); break;
                    case 6: inv.StackAll(); break;
                    case 7: inv.SetCapacity(random.NextInt(1, 201)); break;
                }
                if (!success) f.SameSlots(before);
                if (operation >= 4) for (int i = 0; i < counts.Length; i++) Assert.AreEqual(counts[i], inv.CountOf(f.Items[i].Id));
                f.Invariants();
            }
        }
        [Test] public void CanApply_Shortfall()
        {
            using var f = new Fixture(); f.Add(0, 3); var before = f.Snapshot(); var shortfalls = new List<ItemShortfall>();
            Assert.IsFalse(f.Inventory.CanApply(new[] { new ItemDelta(f.Items[0].Id, -4) }, shortfalls, out bool full));
            Assert.IsFalse(full); Assert.AreEqual(1, shortfalls.Count); Assert.AreEqual(f.Items[0].Id, shortfalls[0].Item);
            Assert.AreEqual(1, shortfalls[0].Missing); f.SameSlots(before);
        }
        [Test] public void CanApply_CargoFull()
        {
            using var f = new Fixture(); f.Inventory.SetCapacity(1); f.Add(0, 10); var before = f.Snapshot();
            var shortfalls = new List<ItemShortfall>();
            Assert.IsFalse(f.Inventory.CanApply(new[] { new ItemDelta(f.Items[1].Id, 10) }, shortfalls, out bool full));
            Assert.IsTrue(full); Assert.AreEqual(0, shortfalls.Count); f.SameSlots(before);
        }
        [Test] public void ApplyAtomic_NoPartial()
        {
            using var f = new Fixture(); f.Inventory.SetCapacity(1); f.Add(0, 3); var before = f.Snapshot();
            Assert.IsFalse(f.Inventory.TryApplyAtomic(new[] { new ItemDelta(f.Items[0].Id, -4), new ItemDelta(f.Items[1].Id, 1) })); f.SameSlots(before);
            Assert.IsFalse(f.Inventory.TryApplyAtomic(new[] { new ItemDelta(f.Items[0].Id, -3), new ItemDelta(f.Items[1].Id, 11) })); f.SameSlots(before);
            Assert.IsTrue(f.Inventory.TryApplyAtomic(new[] { new ItemDelta(f.Items[1].Id, 10), new ItemDelta(f.Items[0].Id, -3) }));
            Assert.AreEqual(0, f.Inventory.CountOf(f.Items[0].Id)); Assert.AreEqual(10, f.Inventory.CountOf(f.Items[1].Id));
            Assert.IsTrue(f.Inventory.TryApplyAtomic(new[] { new ItemDelta(f.Items[1].Id, -12), new ItemDelta(f.Items[1].Id, 2) }));
            Assert.AreEqual(0, f.Inventory.UsedSlots);
        }
        [Test] public void Give_OverflowThenJettison()
        {
            using var f = new Fixture(); f.Inventory.SetCapacity(1);
            int notices = 0, acquired = 0;
            Action<EconomyNotice> notice = value => { if (value.Kind == EconomyNoticeKind.Jettisoned) notices++; };
            Action<ItemAcquired> acquire = value => acquired += value.Count;
            EventBus<EconomyNotice>.Drain(); EventBus<ItemAcquired>.Drain();
            EventBus<EconomyNotice>.Subscribe(notice); EventBus<ItemAcquired>.Subscribe(acquire);
            try
            {
                LogAssert.Expect(LogType.Warning, "[Economy] Hold full: 7 × Item 00 jettisoned.");
                f.Inventory.Give(f.Items[0], 2007, AcquireSource.Narrative);
                EventBus<EconomyNotice>.Drain(); EventBus<ItemAcquired>.Drain();
                Assert.AreEqual(1, notices); Assert.AreEqual(2000, acquired); Assert.AreEqual(200, f.Inventory.UsedSlots);
                Assert.AreEqual(10, f.Inventory.Get(199).Count); Assert.AreEqual(2000, f.Inventory.CountOf(f.Items[0].Id));
            }
            finally { EventBus<EconomyNotice>.Unsubscribe(notice); EventBus<ItemAcquired>.Unsubscribe(acquire); }
        }
        [Test] public void Capacity_DropKeepsItems()
        {
            using var f = new Fixture(); f.Inventory.SetCapacity(3); f.Add(0, 25); var before = f.Snapshot();
            f.Inventory.SetCapacity(1); f.SameSlots(before); Assert.AreEqual(25, f.Inventory.CountOf(f.Items[0].Id));
            Assert.IsTrue(f.Inventory.TryTake(f.Items[0], 6)); Assert.IsTrue(f.Inventory.Get(2).IsEmpty);
            Assert.AreEqual(9, f.Inventory.Get(1).Count); Assert.AreEqual(10, f.Inventory.Get(0).Count);
        }
        [Test] public void Move_IntoOverflowRefused()
        {
            using var f = new Fixture(); f.Inventory.SetCapacity(2); f.Add(0, 10); f.Add(1, 5); f.Inventory.SetCapacity(1);
            var before = f.Snapshot(); Assert.IsFalse(f.Inventory.TryMove(0, 1)); f.SameSlots(before);
            Assert.IsTrue(f.Inventory.TryMove(1, 0)); Assert.AreEqual(f.Items[1], f.Inventory.Get(0).Item);
            Assert.AreEqual(f.Items[0], f.Inventory.Get(1).Item); f.Invariants();
        }
        [Test] public void Sort_KeepsCounts()
        {
            using var f = new Fixture(); f.Add(2, 13); f.Add(0, 18); f.Add(1, 7); f.Inventory.SetCapacity(1);
            f.Inventory.Sort(); f.Invariants(); Assert.AreEqual(18, f.Inventory.CountOf(f.Items[0].Id));
            Assert.AreEqual(7, f.Inventory.CountOf(f.Items[1].Id)); Assert.AreEqual(13, f.Inventory.CountOf(f.Items[2].Id));
            Assert.AreEqual(f.Items[0], f.Inventory.Get(0).Item); Assert.AreEqual(10, f.Inventory.Get(0).Count);
            Assert.AreEqual(8, f.Inventory.Get(1).Count); Assert.AreEqual(f.Items[1], f.Inventory.Get(2).Item);
        }
        [Test] public void Events_PerSlotAndCounts()
        {
            using var f = new Fixture(); var inv = f.Inventory; int slots = 0, items = 0, resets = 0, changed = 0;
            inv.SlotChanged += _ => { slots++; Assert.AreEqual(15, inv.CountOf(f.Items[0].Id)); };
            inv.ItemCountChanged += _ => items++; inv.Reset += () => resets++;
            inv.Changed += () => { changed++; Assert.IsTrue(inv.CanApply(ReadOnlySpan<ItemDelta>.Empty, null, out _)); };
            f.Add(0, 15); Assert.AreEqual(2, slots); Assert.AreEqual(2, items); Assert.AreEqual(1, changed);
            Assert.IsFalse(inv.TryTake(f.Items[0], 16)); Assert.AreEqual(1, changed);
            inv.Sort(); Assert.AreEqual(2, slots); Assert.AreEqual(1, resets); Assert.AreEqual(2, changed);
            inv.SetCapacity(1); Assert.AreEqual(2, resets); Assert.AreEqual(3, changed);
            inv.ResetToEmpty(); Assert.AreEqual(3, resets); Assert.AreEqual(4, changed); Assert.AreEqual(0, inv.UsedSlots);
        }
        [Test] public void CountOf_NoAlloc()
        {
            using var f = new Fixture(); f.Add(0, 7); var id = f.Items[0].Id; int index = f.Catalog.IndexOf<ItemDefinition>(f.Items[0]);
            int sum = 0; for (int i = 0; i < 100; i++) sum += f.Inventory.CountOf(id) + f.Inventory.CountOf(index);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) sum += f.Inventory.CountOf(id) + f.Inventory.CountOf(index);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(141400, sum); Assert.AreEqual(0, allocated);
        }
    }
}
