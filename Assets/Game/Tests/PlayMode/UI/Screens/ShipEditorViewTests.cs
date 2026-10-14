using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Core.Events;
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
    /// <summary>HUD-018: four required cases over real mounted panels/root/pointer gate; bounded layout frames.</summary>
    public sealed class ShipEditorViewTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); }
        sealed class Frames : IFrameClock { public int FrameCount { get; set; } = 10; public float UnscaledTime => 0; }
        sealed class Commands : IShipEditorCommands
        {
            internal int BackCalls, Tool;
            public bool OnBackPressed() { BackCalls++; return true; }
            internal int Hotbar = -1, PaletteToggles, DetailToggles;
            public void SelectHotbar(int slot) => Hotbar = slot;
            public void TogglePalette() => PaletteToggles++;
            public void ToggleDetails() => DetailToggles++;
            public void SelectTool(int tool) => Tool = tool;
            public void SelectBlock(string block) { }
            public void SelectColor(int index) { }
            public void SelectCategory(int index) { }
            public void SetSearch(string text) { }
            public void Undo() { }
            public void Redo() { }
            public void ToggleMirror(int axis) { }
            public void NudgeMirrorPlane(int axis, int halfCells) { }
            public void ToggleSlice() { }
            public void StepSlice(int delta) { }
            public void ToggleXRay() { }
            public void CycleBoxMode() { }
            public void FocusIssue(int index) { }
            public void RemoveFloatingParts() { }
            public void RevertAll() { }
            public void SaveBlueprint() { }
            public void OpenBlueprintBrowser() { }
            public void RequestCommit() { }
            public void RequestLeave() { }
            public void OpenSettings() { }
        }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly Frames Clock = new();
            internal readonly Commands Commands = new();
            internal readonly DialogTestContext.Context Context = new();
            internal readonly DialogTestContext.Pause Pause = new();
            internal readonly FlightHudView Flight;
            internal readonly ShipEditorView Editor;
            readonly UIConfig config;
            internal Window(int width = 1280, int height = 720)
            {
                Panel = new UITestPanel(width, height); config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset"));
                config.PanelSettings = Panel.Settings;
                // The actual FlightHudView shell can use the existing authored empty root until HUD-007 ships its content.
                if (!Array.Exists(config.Screens, entry => entry.Key == "hud"))
                {
                    var screens = new List<ScreenAsset>(config.Screens); screens.Add(new ScreenAsset { Key = "hud", Uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Game/UI/Views/Uxml/M1SceneRoot.uxml") });
                    config.Screens = screens.ToArray();
                }
                UI = new UIRoot(Panel.Document, config, Pause, Context, new NullUISoundSink(), Clock);
                Flight = new FlightHudView(new HudViewModel(), null, _ => ""); Editor = new ShipEditorView(EditorPanelsTests.WorstCase(), Commands);
                UI.Register(Flight); UI.Register(Editor); UI.Navigator.SetRoot(Flight);
            }
            internal void ShowEditor() => UI.Navigator.SetRoot(Editor);
            public void Dispose() { UI.Dispose(); Object.DestroyImmediate(config); Panel.Dispose(); }
        }
        static VisualElement Region(Window w, string name) => w.Editor.Root.Q("ship-editor-region-" + name);
        [UnityTest] public IEnumerator SetRoot_OnlyEditorRootVisible()
        {
            using var w = new Window(); w.ShowEditor(); yield return null;
            Assert.IsTrue(w.Flight.Root.ClassListContains(UIStyleClasses.IsHidden)); Assert.IsFalse(w.Editor.Root.ClassListContains(UIStyleClasses.IsHidden));
            Assert.AreEqual(DisplayStyle.None, w.Flight.Root.resolvedStyle.display); Assert.AreEqual(DisplayStyle.Flex, w.Editor.Root.resolvedStyle.display);
            Assert.AreSame(w.Editor, w.UI.Navigator.Top); Assert.IsFalse(w.UI.Navigator.IsOpen<FlightHudView>()); Assert.IsTrue(w.UI.Navigator.IsOpen<ShipEditorView>());
            Assert.AreEqual(InputContext.Editor, w.Context.Current); Assert.AreEqual(0, w.Pause.ActiveTokenCount);
            Assert.IsNotNull(w.Editor.Root.Q<Button>("editor-tool-place")); Assert.IsNotNull(w.Editor.Root.Q("editor-palette-grid"));
            Assert.IsNotNull(w.Editor.Root.Q("editor-stats")); Assert.IsNotNull(w.Editor.Root.Q("editor-status"));
            var toolbar = w.Editor.Root.Q("editor-toolbar"); w.UI.Navigator.SetRoot(w.Flight); w.ShowEditor();
            Assert.AreSame(toolbar, w.Editor.Root.Q("editor-toolbar")); Assert.AreEqual(1, w.Editor.Root.Query("editor-toolbar").ToList().Count);
            DialogTestContext.Activate(w.Editor.Root.Q<Button>("editor-tool-paint")); Assert.AreEqual(7, w.Commands.Tool);
        }
        [UnityTest] public IEnumerator Centre_NotPickable()
        {
            using var w = new Window(); w.ShowEditor(); w.Clock.FrameCount += 2; yield return null;
            var left = Region(w, "left").worldBound; var right = Region(w, "right").worldBound;
            var top = Region(w, "top").worldBound; var bottom = Region(w, "bottom").worldBound;
            var centre = new Vector2((left.xMax + right.xMin) * .5f, (top.yMax + bottom.yMin) * .5f);
            Assert.AreEqual(PickingMode.Ignore, w.Editor.Root.pickingMode); Assert.IsNull(w.Editor.Root.panel.Pick(centre));
            float panelHeight = w.Editor.Root.panel.visualTree.layout.height;
            Assert.IsTrue(w.UI.IsWorldPointerAllowed(new Vector2(centre.x, panelHeight - centre.y)));
            foreach (string name in new[] { "top", "left", "right", "bottom" })
            {
                var region = Region(w, name); var point = region.worldBound.center;
                Assert.IsNotNull(w.Editor.Root.panel.Pick(point), name); Assert.IsFalse(w.UI.IsWorldPointerAllowed(new Vector2(point.x, panelHeight - point.y)), name);
            }
        }
        [UnityTest] public IEnumerator Regions_NoOverlap_AllLogicalSizes()
        {
            var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var w = new Window(size.x, size.y); w.ShowEditor(); yield return null;
                var regions = new[] { Region(w, "top"), Region(w, "left"), Region(w, "right"), Region(w, "bottom") };
                // QA-001 creative layout: slim toolbar, catalogue drawer, compact stats card, hotbar + paint strip.
                Assert.AreEqual(40, regions[0].worldBound.height, .1f); Assert.AreEqual(360, regions[1].worldBound.width, .1f);
                Assert.AreEqual(320, regions[2].worldBound.width, .1f); Assert.AreEqual(112, regions[3].worldBound.height, .1f);
                for (int i = 0; i < regions.Length; i++)
                {
                    Assert.IsTrue(regions[i].ClassListContains(UIStyleClasses.HudRegion)); Assert.IsTrue(regions[i].ClassListContains("hud-plate"));
                    for (int j = i + 1; j < regions.Length; j++) Assert.IsFalse(regions[i].worldBound.Overlaps(regions[j].worldBound), size + " " + i + "/" + j);
                }
                Assert.Greater(regions[2].worldBound.xMin - regions[1].worldBound.xMax, 0);
                UILayoutAudit.Run(w.Editor.Root, w.Panel.Viewport, issues);
                Assert.IsEmpty(issues, size + ": " + string.Join(", ", issues.ConvertAll(issue => issue.Kind + " " + issue.ElementPath)));
            }
        }
        [Test] public void Back_ForwardsToCommands()
        {
            using var w = new Window(); w.ShowEditor(); Assert.IsTrue(w.Editor.OnBack()); Assert.AreEqual(1, w.Commands.BackCalls);
            w.UI.HandleBackPressed(); Assert.AreEqual(2, w.Commands.BackCalls); Assert.AreSame(w.Editor, w.UI.Navigator.Top);
            Assert.AreEqual(0, w.Pause.ActiveTokenCount);
        }
    }
}
