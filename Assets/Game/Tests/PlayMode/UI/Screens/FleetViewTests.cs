using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Game.App.EditorHost;
using Game.App.Presenters;
using Game.App.Session;
using Game.Core;
using Game.Core.Events;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Galaxy.Data;
using Game.Galaxy.Services;
using Game.UI.Controls;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode.UI.Screens
{
    /// <summary>HUD-017: three required cases; two tiny owned ships and real fleet/docking/stats owners.</summary>
    public sealed class FleetViewTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); }
        sealed class Fleet : IDisposable
        {
            readonly List<Object> assets = new();
            readonly BlobAssetReference<BlockDefsBlob> defs;
            readonly BlobAssetReference<BlockModulesBlob> modules;
            readonly BlobAssetReference<OrientationTablesBlob> rotations;
            internal readonly FleetModel Model = new();
            internal readonly DockingService Docking = new();
            internal readonly ContentCatalog Catalog;
            internal readonly BlockDefinition Hull;
            internal readonly BlockDefinition Core;
            internal readonly string LongName = new string('W', 32);
            internal Fleet()
            {
                var definitions = new List<ContentDefinition>();
                foreach (string path in new[] { "Blocks/block_missing", "Blocks/block_hull", "Blocks/block_core_small", "Items/item_missing" })
                { var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ContentDefinition>("Assets/Game/Content/" + path + ".asset")); assets.Add(asset); definitions.Add(asset); }
                var blueprint = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); assets.Add(blueprint);
                using (var edit = new SerializedObject(blueprint))
                { edit.FindProperty("id").stringValue = "ship_fleet_fixture"; edit.FindProperty("displayName").stringValue = "Fixture patrol class"; edit.ApplyModifiedPropertiesWithoutUndo(); }
                definitions.Add(blueprint); var report = new ValidationReport("Fleet screen fixture"); Catalog = ContentCatalog.Build(definitions, report);
                defs = BlockDefsBlobBuilder.Build(Catalog, report, new List<Mesh>(), Allocator.Persistent, out modules); Assert.AreEqual(0, report.ErrorCount, report.ToText());
                rotations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
                Hull = Catalog.Get<BlockDefinition>(new ContentId("block_hull")); Core = Catalog.Get<BlockDefinition>(new ContentId("block_core_small"));
                Model.Add(Ship(1, LongName), true); Model.Add(Ship(2, "Secondary vessel"), false);
            }
            FleetShipRecord Ship(ulong id, string name)
            {
                var structure = new VoxelStructure(); using var touched = new NativeList<int3>(Allocator.Temp);
                using var edits = new NativeArray<VoxelEdit>(new[] {
                    new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = Catalog.GetBlockIndex(Core) },
                    new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = Catalog.GetBlockIndex(Hull), Cell = new int3(1, 0, 0) } }, Allocator.Temp);
                Assert.IsTrue(structure.ApplyEdits(edits, new VoxelContent { Blocks = defs, Orientations = rotations }, touched));
                return new FleetShipRecord { PersistentId = id, Name = name, SourceBlueprint = new ContentId("ship_fleet_fixture"), Structure = structure };
            }
            public void Dispose() { Model.Dispose(); rotations.Dispose(); modules.Dispose(); defs.Dispose(); foreach (var asset in assets) Object.DestroyImmediate(asset); }
        }
        sealed class Window : IDisposable
        {
            internal readonly UITestPanel Panel;
            internal readonly UIRoot UI;
            internal readonly FleetViewModel Model = new();
            internal readonly FleetPresenter Presenter;
            internal readonly FleetView View;
            internal string Notice;
            readonly UIConfig config;
            internal Window(Fleet fleet, int width = 1280, int height = 720, ShipEditorLauncher launcher = null)
            {
                Panel = new UITestPanel(width, height); config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); config.PanelSettings = Panel.Settings;
                UI = new UIRoot(Panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
                Presenter = new FleetPresenter(Model, fleet.Catalog, launcher, text => Notice = text); Presenter.Bind(fleet.Model, fleet.Docking);
                View = new FleetView(Model, Presenter); UI.Register(View); UI.Navigator.Push(View);
            }
            public void Dispose() { UI.Dispose(); Presenter.Dispose(); Object.DestroyImmediate(config); Panel.Dispose(); }
        }
        [UnityTest] public IEnumerator Name_DisplayNameEllipsized()
        {
            using var fleet = new Fleet(); using var w = new Window(fleet); yield return null;
            var name = w.View.Root.Q<Label>("fleet-ship-row-0-name"); Assert.IsNotNull(name); Assert.AreEqual(fleet.LongName, name.text);
            Assert.AreNotEqual(fleet.Model.Flagship.SourceBlueprint.Value, name.text); Assert.IsTrue(name.ClassListContains(UIStyleClasses.TextEllipsis));
            Assert.AreEqual(TextOverflow.Ellipsis, name.resolvedStyle.textOverflow); Assert.Less(name.worldBound.width, name.MeasureTextSize(name.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x);
            Assert.AreEqual(2, w.Model.Ships[0].BlockCount); Assert.AreEqual(1, w.Model.Ships[0].HullFraction, .001f);
            double mass = BlockStatMath.Of(fleet.Core, 0).Mass + BlockStatMath.Of(fleet.Hull, 0).Mass;
            Assert.AreEqual(mass.ToString("0.#", CultureInfo.InvariantCulture) + " kg", w.Model.DetailStats.Find(row => row.StatId == "mass").Value);
            Assert.AreEqual("Fixture patrol class", w.Model.DetailClass);
            w.View.Root.Q<ListView>("fleet-ship-list").SetSelection(1); Assert.AreEqual("Secondary vessel", w.Model.DetailName);
            w.UI.Navigator.Pop(w.View); fleet.Model.Ships[0].Name = "Changed while closed"; w.Presenter.Refresh(); Assert.AreEqual(fleet.LongName, name.text, "Closed row releases its model subscription.");
            w.UI.Navigator.Push(w.View); Assert.AreEqual("Changed while closed", name.text);
        }
        [UnityTest] public IEnumerator Layout_StatsNoOverlap_AllLogicalSizes()
        {
            using var fleet = new Fleet(); var issues = new List<LayoutIssue>();
            foreach (var size in UITestPanel.LogicalSizes)
            {
                using var w = new Window(fleet, size.x, size.y); yield return null;
                var heading = w.View.Root.Q("fleet-detail-heading"); var list = w.View.Root.Q<ListView>("fleet-stats");
                Assert.AreEqual(36, list.fixedItemHeight); Assert.GreaterOrEqual(list.worldBound.yMin, heading.worldBound.yMax - .1f);
                Assert.AreEqual(11, w.Model.DetailStats.Count); Assert.IsNotNull(w.View.Root.Q("fleet-stat-row-0"));
                UILayoutAudit.Run(w.View.Root, w.Panel.Viewport, issues); Assert.IsEmpty(issues, size + ": " + string.Join(", ", issues.ConvertAll(issue => issue.Kind + " " + issue.ElementPath)));
                list.ScrollToItem(w.Model.DetailStats.Count - 1); yield return null;
                Assert.GreaterOrEqual(list.worldBound.yMin, heading.worldBound.yMax - .1f);
                UILayoutAudit.Run(w.View.Root, w.Panel.Viewport, issues); Assert.IsEmpty(issues, size + " scrolled");
            }
        }
        [Test] public void NotDocked_EditDisabledWithReason()
        {
            using var fleet = new Fleet(); using var w = new Window(fleet);
            Assert.IsFalse(w.Model.CanEdit); Assert.AreEqual("Dock at a station to edit", w.Model.EditBlockedReason);
            Assert.IsFalse(w.View.Root.Q<Button>("fleet-edit").enabledSelf); Assert.AreEqual(w.Model.EditBlockedReason, w.View.Root.Q<Label>("fleet-edit-blocked").text);
            w.Presenter.Edit(); Assert.AreEqual("Dock at a station to edit", w.Notice);
            fleet.Docking.Dock(new BodyId(new StarSystemId(1), BodyRole.Station, 0, 0));
            // Composition has not supplied EDT-023's launcher: docking cannot manufacture successful launch support.
            w.Presenter.Edit(); Assert.IsFalse(w.Model.CanEdit); Assert.AreEqual("Ship editing is not available yet", w.Notice);
            Assert.IsFalse(w.Model.CanSetActive); Assert.IsFalse(w.View.Root.Q<Button>("fleet-set-active").enabledSelf);
            var flagship = fleet.Model.Flagship; w.Presenter.SetActive(); Assert.AreSame(flagship, fleet.Model.Flagship);
            w.Presenter.Select(1); Assert.AreEqual("Only the active ship can be edited", w.Model.EditBlockedReason);
            fleet.Docking.Undock(); Assert.AreEqual("Dock at a station to edit", w.Model.EditBlockedReason);
            // Old fleet/docking notifications cannot restore rows after a session unbind.
            w.Presenter.Unbind(); fleet.Docking.Dock(new BodyId(new StarSystemId(1), BodyRole.Station, 0, 0)); Assert.IsEmpty(w.Model.Ships); Assert.IsFalse(w.Model.CanEdit);
        }
    }
}
