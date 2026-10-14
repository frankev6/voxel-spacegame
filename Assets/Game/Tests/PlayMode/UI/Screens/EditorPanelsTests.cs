using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
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
    /// <summary>EDT-021: four required cases, real authored fragments/theme/panel, bounded layout frames.</summary>
    public sealed class EditorPanelsTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); }
        sealed class Commands : IShipEditorCommands
        {
            internal int Tool, ToolCalls, Color = -1, Category = -1, Issue = -1, UndoCalls, CommitCalls, NudgeAxis = -1, Nudge;
            internal string Block, Search;
            public void SelectTool(int id) { Tool = id; ToolCalls++; }
            public void SelectBlock(string id) => Block = id;
            public void SelectColor(int index) => Color = index;
            public void SelectCategory(int index) => Category = index;
            public void SetSearch(string text) => Search = text;
            public void Undo() => UndoCalls++;
            public void Redo() { }
            public void ToggleMirror(int axis) { }
            public void NudgeMirrorPlane(int axis, int value) { NudgeAxis = axis; Nudge = value; }
            public void ToggleSlice() { }
            public void StepSlice(int delta) { }
            public void ToggleXRay() { }
            public void CycleBoxMode() { }
            public void FocusIssue(int index) => Issue = index;
            public void RemoveFloatingParts() { }
            public void RevertAll() { }
            public void SaveBlueprint() { }
            public void OpenBlueprintBrowser() { }
            public void RequestCommit() => CommitCalls++;
            public void RequestLeave() { }
            public void OpenSettings() { }
            public bool OnBackPressed() => true;
        }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly Commands Commands = new();
            internal readonly ShipEditorViewModel Model = WorstCase();
            internal readonly VisualElement Root, Top, Left, Right, Bottom;
            readonly UIConfig config;
            internal Window(int width = 1280, int height = 720)
            {
                Panel = new UITestPanel(width, height);
                config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                Root = new VisualElement { name = "editor-panel-test", pickingMode = PickingMode.Ignore }; Root.AddToClassList(UIStyleClasses.Screen);
                UI.GetLayer(UILayer.Hud).Add(Root);
                Top = Region("ship-editor-region-top"); Top.style.top = 0; Top.style.left = 0; Top.style.right = 0; Top.style.height = 64;
                Bottom = Region("ship-editor-region-bottom"); Bottom.style.bottom = 0; Bottom.style.left = 0; Bottom.style.right = 0; Bottom.style.height = 48;
                Left = Region("ship-editor-region-left"); Left.style.left = 0; Left.style.top = 64; Left.style.bottom = 48; Left.style.width = 360;
                Right = Region("ship-editor-region-right"); Right.style.right = 0; Right.style.top = 64; Right.style.bottom = 48; Right.style.width = 380;
                ShipEditorPanels.Mount(Top, Left, Right, Bottom, Model, Commands, UI);
            }
            VisualElement Region(string name)
            {
                var region = new VisualElement { name = name }; region.AddToClassList(UIStyleClasses.HudRegion); region.AddToClassList("hud-plate"); Root.Add(region); return region;
            }
            internal void Click(string name)
            {
                var button = Root.Q<Button>(name); Assert.IsNotNull(button, name); DialogTestContext.Activate(button);
            }
            public void Dispose() { Root.RemoveFromHierarchy(); UI.Dispose(); Object.Destroy(config); Panel.Dispose(); }
        }
        internal static ShipEditorViewModel WorstCase()
        {
            var vm = new ShipEditorViewModel { Title = "Blueprint Lab", ActiveTool = 1, IsCreative = false, CanUndo = true, CanRedo = true,
                UndoLabel = "Undo place hull", RedoLabel = "Redo place hull", CanCommit = true, CommitLabel = "Commit", IsDirty = true,
                SliceEnabled = true, SliceLevel = 12, OrientationText = "Yaw 180°", HoverText = "Hull · 12, 0, 18", StatusText = "Ready to build",
                Palette = new EditorPaletteViewModel(), Stats = new EditorStatsViewModel(), Bom = new EditorBomViewModel(), Issues = new EditorIssuesViewModel() };
            vm.Palette.Categories.AddRange(new[] { "Structure", "Detail", "Core", "Propulsion", "Power", "Weapons", "Shields", "Utility" });
            for (int i = 0; i < 128; i++) vm.Palette.Items.Add(new EditorPaletteItemViewModel
                { BlockId = "block_test_" + i, DisplayName = "Reinforced hull " + i, Available = i, ShowCount = true, IsLocked = i == 1, LockReason = i == 1 ? "Not obtainable" : "" });
            for (int i = 0; i < 16; i++) vm.Palette.Colors[i].Swatch = Color.HSVToRGB(i / 16f, .5f, .8f);
            for (int i = 0; i < 12; i++) vm.Stats.Rows.Add(new StatRowViewModel { StatId = "stat_" + i, Label = "Forward thrust " + i,
                Value = "999.9 kN", Delta = "+10.0", DeltaSign = i % 2 == 0 ? 1 : -1 });
            for (int i = 0; i < 64; i++) vm.Bom.Rows.Add(new EditorBomRowViewModel { ItemId = "block_test_" + i, Name = "Reinforced hull " + i,
                Required = 12, Refunded = 3, Available = 8, Shortfall = 4, LostDamaged = 2 });
            vm.Bom.SummaryText = "Uses 768 blocks · refunds 192 · 128 damaged lost"; vm.Bom.TotalShortfall = 256; vm.Bom.HasShortfall = true;
            for (int i = 0; i < 16; i++) vm.Issues.Rows.Add(new EditorIssueRowViewModel { Index = i, Text = "Disconnected blocks in island " + i, IsError = i < 8 });
            vm.Issues.ErrorCount = 8; vm.Issues.WarningCount = 8; vm.Issues.CanRemoveFloating = true; return vm;
        }
        static HashSet<VisualElement> Elements(VisualElement root)
        {
            var result = new HashSet<VisualElement>(); Visit(root); return result;
            void Visit(VisualElement element) { result.Add(element); for (int i = 0; i < element.hierarchy.childCount; i++) Visit(element.hierarchy[i]); }
        }
        [UnityTest] public IEnumerator AllNamedElementsExist()
        {
            using var w = new Window(); yield return null;
            string[] tools = { "place", "line", "box", "fill", "remove", "replace", "paint", "eyedropper", "select" };
            foreach (string tool in tools) Assert.IsNotNull(w.Root.Q<Button>("editor-tool-" + tool));
            string[] names = { "editor-undo", "editor-redo", "editor-slice-toggle", "editor-slice-down", "editor-slice-up", "editor-slice-level",
                "editor-xray", "editor-box-mode", "editor-orientation", "editor-save-blueprint", "editor-load-blueprint", "editor-settings", "editor-leave",
                "editor-palette-tabs", "editor-palette-search", "editor-palette-grid", "editor-colors", "editor-stats", "editor-bom", "editor-bom-summary",
                "editor-issues", "editor-remove-floating", "editor-revert", "editor-commit", "editor-hover-info", "editor-status", "editor-key-hints" };
            foreach (string name in names) Assert.IsNotNull(w.Root.Q(name), name);
            foreach (char axis in "xyz") foreach (string suffix in new[] { "", "-minus", "-plus" }) Assert.IsNotNull(w.Root.Q("editor-mirror-" + axis + suffix));
            for (int i = 0; i < 128; i++) Assert.IsNotNull(w.Root.Q<Slot>("editor-palette-block_test_" + i));
            for (int i = 0; i < 16; i++) { Assert.IsNotNull(w.Root.Q<Button>("editor-color-" + i)); Assert.IsNotNull(w.Root.Q<Button>("editor-issue-" + i)); }
            for (int i = 0; i < 12; i++) Assert.IsNotNull(w.Root.Q("editor-stat-stat_" + i));
            for (int i = 0; i < 64; i++) Assert.IsNotNull(w.Root.Q("editor-bom-block_test_" + i));
            Assert.IsTrue(w.Root.Q<Label>("editor-stat-delta-0").ClassListContains("editor-delta--better"));
            Assert.AreEqual("999.9 kN", w.Root.Q<Label>("editor-stat-value-0").text);
            w.Model.Stats.Rows[0].Value = "120.0 kN";
            Assert.AreEqual("120.0 kN", w.Root.Q<Label>("editor-stat-value-0").text, "Individual row notifications update the pooled value label.");
            Assert.IsTrue(w.Root.Q<Label>("editor-bom-count-0-3").ClassListContains("editor-shortfall"));
            Assert.AreEqual("0", w.Root.Q<Slot>("editor-palette-block_test_0").Q<Label>(className: UIStyleClasses.SlotCount).text);
        }
        [UnityTest] public IEnumerator Layout_WorstCase_AllLogicalSizes_NoIssues()
        {
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var w = new Window(size.x, size.y); yield return null;
                Assert.Greater(w.Root.Q<ScrollView>("editor-bom").worldBound.height, 0);
                UILayoutAudit.Run(w.Root, w.Panel.Viewport, issues);
                Assert.IsEmpty(issues, size + ": " + string.Join(", ", issues.ConvertAll(issue => issue.Kind + " " + issue.ElementPath)));
                w.Root.Q<ScrollView>("editor-palette-grid").scrollOffset = new Vector2(0, 10000);
                w.Root.Q<ScrollView>("editor-bom").scrollOffset = new Vector2(0, 10000);
                w.Root.Q<ScrollView>("editor-issues").scrollOffset = new Vector2(0, 10000); yield return null;
                UILayoutAudit.Run(w.Root, w.Panel.Viewport, issues); Assert.IsEmpty(issues, size + " scrolled");
            }
        }
        [UnityTest] public IEnumerator NoElementsCreatedAfterMount()
        {
            using var w = new Window(); var original = Elements(w.Root); yield return null;
            Assert.IsTrue(original.SetEquals(Elements(w.Root)), "The initial layout creates no additional panel elements.");
            var oldRow = w.Model.Palette.Items[0];
            w.Model.Palette.Items.Clear(); w.Model.Palette.NotifyItems(); w.Model.Bom.Rows.Clear(); w.Model.Bom.NotifyRows();
            w.Model.Issues.Rows.Clear(); w.Model.Issues.NotifyRows(); w.Model.Stats.Rows.Clear(); w.Model.Stats.NotifyRows();
            var next = WorstCase(); w.Model.Palette = next.Palette; w.Model.Stats = next.Stats; w.Model.Bom = next.Bom; w.Model.Issues = next.Issues;
            w.Model.Palette.Items.Add(new EditorPaletteItemViewModel { BlockId = "block_page_2", DisplayName = "Page two", ShowCount = true, Available = 1 }); w.Model.Palette.NotifyItems();
            w.Model.Bom.Rows.Add(new EditorBomRowViewModel { ItemId = "block_page_2", Name = "Page two" }); w.Model.Bom.NotifyRows();
            w.Model.Issues.Rows.Add(new EditorIssueRowViewModel { Index = 99, Text = "Page two issue", IsError = true }); w.Model.Issues.NotifyRows();
            w.Click("editor-palette-grid-next"); w.Click("editor-bom-next"); w.Click("editor-issues-next");
            Assert.IsNotNull(w.Root.Q<Slot>("editor-palette-block_page_2")); Assert.IsNotNull(w.Root.Q("editor-bom-block_page_2"));
            w.Click("editor-issue-99"); Assert.AreEqual(99, w.Commands.Issue);
            var slot = w.Root.Q<Slot>("editor-palette-block_page_2"); using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = slot; slot.SendEvent(submit); }
            Assert.AreEqual("block_page_2", w.Commands.Block, "Pooled callbacks use current identity.");
            w.Click("editor-palette-grid-previous"); Assert.IsNotNull(w.Root.Q<Slot>("editor-palette-block_test_0"));
            oldRow.BlockId = "stale_block"; Assert.IsNull(w.Root.Q("editor-palette-stale_block"));
            yield return null; Assert.IsTrue(original.SetEquals(Elements(w.Root)), "Rebinding, paging and scrolling preserve every element identity.");
            w.Root.RemoveFromHierarchy(); w.Model.StatusText = "Detached";
            Assert.AreNotEqual("Detached", w.Root.Q<Label>("editor-status").text, "Detached panels release model subscriptions.");
            w.UI.GetLayer(UILayer.Hud).Add(w.Root); Assert.AreEqual("Detached", w.Root.Q<Label>("editor-status").text);
        }
        [UnityTest] public IEnumerator ToolButton_CallsSelectTool()
        {
            using var w = new Window(); yield return null;
            string[] tools = { "place", "line", "box", "fill", "remove", "replace", "paint", "eyedropper", "select" };
            for (int i = 0; i < tools.Length; i++) { w.Click("editor-tool-" + tools[i]); Assert.AreEqual(i + 1, w.Commands.Tool); Assert.AreEqual(i + 1, w.Commands.ToolCalls); }
            w.Model.ActiveTool = 7; Assert.IsTrue(w.Root.Q<Button>("editor-tool-paint").ClassListContains(UIStyleClasses.IsSelected));
            w.Click("editor-color-5"); Assert.AreEqual(5, w.Commands.Color);
            w.Root.Q<TabBar>("editor-palette-tabs").SelectedIndex = 2; Assert.AreEqual(2, w.Commands.Category);
            w.Root.Q<TextField>("editor-palette-search").value = "Hull"; Assert.AreEqual("Hull", w.Commands.Search);
            w.Click("editor-mirror-z-minus"); Assert.AreEqual(2, w.Commands.NudgeAxis); Assert.AreEqual(-1, w.Commands.Nudge);
            w.Click("editor-undo"); Assert.AreEqual(1, w.Commands.UndoCalls); w.Model.CanUndo = false; w.Click("editor-undo"); Assert.AreEqual(1, w.Commands.UndoCalls);
            w.Model.CanCommit = false; w.Model.CommitBlockedReason = "Missing hull"; w.Click("editor-commit"); Assert.AreEqual(0, w.Commands.CommitCalls);
            w.Model.CanCommit = true; w.Click("editor-commit"); Assert.AreEqual(1, w.Commands.CommitCalls);
            var locked = w.Root.Q<Slot>("editor-palette-block_test_1"); Assert.IsFalse(locked.enabledInHierarchy);
            Assert.Throws<InvalidOperationException>(() => ShipEditorPanels.Mount(w.Top, w.Left, w.Right, w.Bottom, w.Model, w.Commands, w.UI));
        }
    }
}
