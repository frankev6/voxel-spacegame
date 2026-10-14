using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>HUD-011: five required cases, two real item definitions, bounded UI layout frames.</summary>
    public sealed class InventoryViewTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); }
        sealed class Cargo : IDisposable
        {
            readonly List<Object> owned = new();
            internal readonly InventoryModel Inventory;
            internal readonly Wallet Wallet = new();
            internal readonly ItemDefinition A, B;
            internal Cargo()
            {
                var missingBlock = ScriptableObject.CreateInstance<BlockDefinition>(); owned.Add(missingBlock); Id(missingBlock, "block_missing");
                var missingItem = Item("item_missing", "Missing", 10);
                A = Item("item_inventory_a", "Long reinforced component for a ship", 10); B = Item("item_inventory_b", "Heavy component", 1);
                var report = new ValidationReport("Inventory screen fixture");
                var catalog = ContentCatalog.Build(new ContentDefinition[] { missingBlock, missingItem, A, B }, report);
                Assert.AreEqual(0, report.ErrorCount, report.ToText()); Inventory = new InventoryModel(catalog); Wallet.Set(1_234_567_890);
            }
            static void Id(ContentDefinition definition, string id)
            { using var edit = new SerializedObject(definition); edit.FindProperty("id").stringValue = id; edit.ApplyModifiedPropertiesWithoutUndo(); }
            ItemDefinition Item(string id, string title, int maxStack)
            {
                var item = ScriptableObject.CreateInstance<ItemDefinition>(); owned.Add(item); Id(item, id);
                using var edit = new SerializedObject(item); edit.FindProperty("displayName").stringValue = title;
                edit.FindProperty("description").stringValue = "A cargo component. Move it to another slot, sort cargo, or consolidate stacks.";
                edit.FindProperty("maxStack").intValue = maxStack; edit.FindProperty("basePrice").intValue = 42; edit.ApplyModifiedPropertiesWithoutUndo(); return item;
            }
            internal ItemStack[] Snapshot()
            { var result = new ItemStack[InventoryModel.MaxSlots]; for (int i = 0; i < result.Length; i++) result[i] = Inventory.Get(i); return result; }
            internal void Same(ItemStack[] expected)
            {
                for (int i = 0; i < expected.Length; i++)
                { var actual = Inventory.Get(i); Assert.AreEqual(expected[i].Item, actual.Item); Assert.AreEqual(expected[i].Count, actual.Count); Assert.AreEqual(expected[i].OriginalId, actual.OriginalId); }
            }
            public void Dispose() { foreach (var asset in owned) Object.DestroyImmediate(asset); }
        }
        sealed class Window : IDisposable, IInventoryCommands
        {
            readonly UIConfig config;
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly InventoryViewModel Model = new();
            internal readonly InventoryPresenter Presenter;
            internal readonly InventoryView View;
            internal int MoveCalls, From = -1, To = -1;
            internal Window(Cargo cargo, int width = 1280, int height = 720)
            {
                Panel = new UITestPanel(width, height); config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
                config.PanelSettings = Panel.Settings; UI = new UIRoot(Panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                Presenter = new InventoryPresenter(Model); Presenter.Bind(cargo.Inventory, cargo.Wallet); View = new InventoryView(Model, this); UI.Register(View);
            }
            internal void Open() => UI.Navigator.Push(View);
            internal Slot Slot(int index) => View.Root.Q<Slot>("inventory-slot-" + index);
            public void Select(int index) => Presenter.Select(index);
            public void Move(int from, int to) { MoveCalls++; From = from; To = to; Presenter.Move(from, to); }
            public void Sort() => Presenter.Sort();
            public void StackAll() => Presenter.StackAll();
            public void Dispose() { UI.Dispose(); Presenter.Dispose(); Object.DestroyImmediate(config); Panel.Dispose(); }
        }
        static HashSet<VisualElement> Elements(VisualElement root)
        {
            var elements = new HashSet<VisualElement>(); Visit(root); return elements;
            void Visit(VisualElement value) { elements.Add(value); for (int i = 0; i < value.hierarchy.childCount; i++) Visit(value.hierarchy[i]); }
        }
        [UnityTest] public IEnumerator LastSlot_Displayed()
        {
            using var cargo = new Cargo(); cargo.Inventory.SetCapacity(199); cargo.Inventory.Give(cargo.B, 200, AcquireSource.Loot);
            using var w = new Window(cargo); w.Open(); yield return null;
            Assert.AreEqual(200, w.View.Root.Query<Slot>().ToList().Count); Assert.IsTrue(w.Slot(199).ClassListContains("slot--overflow"));
            Assert.IsFalse(w.Slot(199).ClassListContains(UIStyleClasses.IsHidden)); Assert.AreEqual(1, w.Model.OverflowCount);
            var original = Elements(w.View.Root); cargo.Inventory.SetCapacity(200); Assert.IsFalse(w.Slot(199).ClassListContains("slot--overflow"));
            var scroll = w.View.Root.Q<ScrollView>("inventory-grid"); scroll.ScrollTo(w.Slot(199)); yield return null;
            Rect visible = scroll.contentViewport.worldBound, last = w.Slot(199).worldBound;
            Assert.Greater(last.width, 0); Assert.Greater(last.height, 0); Assert.IsTrue(visible.Overlaps(last), "Capacity-1 is reachable in the real scroll viewport.");
            Assert.IsTrue(original.SetEquals(Elements(w.View.Root)), "Capacity changes use the same pool.");
            w.UI.Navigator.Pop(w.View); cargo.Inventory.SetCapacity(10); Assert.IsFalse(w.Slot(199).ClassListContains("slot--overflow"), "Closed view has no row subscriptions.");
            w.Open(); Assert.IsTrue(w.Slot(199).ClassListContains("slot--overflow"));
        }
        [UnityTest] public IEnumerator HeaderButtons_InsidePanel()
        {
            using var cargo = new Cargo(); using var w = new Window(cargo); w.Open(); yield return null;
            var panel = w.View.Root.Q("inventory-panel"); var issues = new List<LayoutIssue>(); UILayoutAudit.Run(w.View.Root, w.Panel.Viewport, issues); Assert.IsEmpty(issues);
            foreach (string id in new[] { "inventory-sort", "inventory-stack-all" })
            {
                var button = w.View.Root.Q<Button>(id); Assert.IsTrue(button.parent.ClassListContains("panel__header"));
                Assert.IsTrue(panel.worldBound.Contains(button.worldBound.min)); Assert.IsTrue(panel.worldBound.Contains(button.worldBound.max - Vector2.one * .01f));
            }
            Assert.IsTrue(w.View.Root.Q<Label>("inventory-wallet").text.Contains("1,234,567,890"));
        }
        [Test] public void Open_NoModelChange()
        {
            using var cargo = new Cargo(); cargo.Inventory.Give(cargo.A, 14, AcquireSource.Loot); cargo.Inventory.SetCapacity(1);
            var before = cargo.Snapshot(); long credits = cargo.Wallet.Credits; int changes = 0;
            cargo.Inventory.Changed += () => changes++;
            using var w = new Window(cargo); w.Open(); w.UI.Navigator.Pop(w.View); w.Open();
            cargo.Same(before); Assert.AreEqual(credits, cargo.Wallet.Credits); Assert.AreEqual(0, changes);
            // Rebind keeps the VM/pool identities and releases the old session's subscriptions.
            var row = w.Model.Slots[0]; using var next = new Cargo(); next.Wallet.Set(2); next.Inventory.Give(next.B, 1, AcquireSource.Loot);
            w.Presenter.Bind(next.Inventory, next.Wallet); Assert.AreSame(row, w.Model.Slots[0]); Assert.AreEqual(next.B.IdString, row.ItemId);
            cargo.Wallet.Set(999); cargo.Inventory.ResetToEmpty(); Assert.AreEqual(2, w.Model.Wallet); Assert.AreEqual(next.B.IdString, row.ItemId);
            w.Presenter.Unbind(); next.Wallet.Set(3); Assert.AreEqual(0, w.Model.Wallet); Assert.AreEqual(0, w.Model.Capacity);
        }
        static void Down(Slot source)
        {
            using var evt = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = source.worldBound.center, button = 0 });
            evt.target = source; source.SendEvent(evt);
        }
        static void Up(Slot source, Vector2 position)
        {
            using var evt = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = position, button = 0 });
            evt.target = source; source.SendEvent(evt);
        }
        [UnityTest] public IEnumerator DragMove_CallsMove()
        {
            using var cargo = new Cargo(); cargo.Inventory.Give(cargo.A, 7, AcquireSource.Loot);
            using var w = new Window(cargo); w.Open(); yield return null;
            var source = w.Slot(0); var destination = w.Slot(1); Down(source); Assert.IsTrue(source.HasPointerCapture(PointerId.mousePointerId));
            Up(source, destination.worldBound.center); Assert.AreEqual(1, w.MoveCalls); Assert.AreEqual(0, w.From); Assert.AreEqual(1, w.To);
            Assert.IsTrue(cargo.Inventory.Get(0).IsEmpty); Assert.AreEqual(7, cargo.Inventory.Get(1).Count); Assert.IsFalse(source.HasPointerCapture(PointerId.mousePointerId));
            Down(destination); Up(destination, new Vector2(-10, -10)); Assert.AreEqual(1, w.MoveCalls);
            Down(destination); w.UI.Navigator.Pop(w.View); Assert.IsFalse(destination.HasPointerCapture(PointerId.mousePointerId));
            w.Open(); Down(destination); cargo.Inventory.ResetToEmpty(); Assert.IsFalse(destination.HasPointerCapture(PointerId.mousePointerId));
            // Exercise the real organisation commands without adding another case.
            cargo.Inventory.Give(cargo.A, 14, AcquireSource.Loot); Assert.IsTrue(cargo.Inventory.TryTake(cargo.A, 3));
            DialogTestContext.Activate(w.View.Root.Q<Button>("inventory-stack-all")); Assert.AreEqual(10, cargo.Inventory.Get(0).Count); Assert.AreEqual(1, cargo.Inventory.Get(1).Count);
            DialogTestContext.Activate(w.View.Root.Q<Button>("inventory-sort")); Assert.AreEqual(11, cargo.Inventory.CountOf(cargo.A.Id));
        }
        [UnityTest] public IEnumerator Layout_AllLogicalSizes_NoIssues()
        {
            using var cargo = new Cargo(); cargo.Inventory.SetCapacity(199); cargo.Inventory.Give(cargo.B, 200, AcquireSource.Loot);
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var w = new Window(cargo, size.x, size.y); w.Open(); w.Select(199); yield return null;
                UILayoutAudit.Run(w.View.Root, w.Panel.Viewport, issues); Assert.IsEmpty(issues, size + ": " + string.Join(", ", issues.ConvertAll(issue => issue.Kind + " " + issue.ElementPath)));
                w.View.Root.Q<ScrollView>("inventory-grid").ScrollTo(w.Slot(199)); yield return null;
                UILayoutAudit.Run(w.View.Root, w.Panel.Viewport, issues); Assert.IsEmpty(issues, size + " scrolled");
            }
        }
    }
}
