using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Game.App.EditorHost;
using Game.Core;
using Game.Core.Events;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Data.Services;
using Game.Data.Validation;
using Game.Persistence.Data;
using Game.Persistence.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.PlayMode.UI;
using Game.UI.Data;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using Game.Voxel.Data;
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

namespace Game.Tests.PlayMode.ShipEditor
{
    /// <summary>EDT-020: three required cases, tiny real content/VOXS/profile fixtures; one browser layout frame.</summary>
    public sealed class BlueprintStoreTests
    {
        [SetUp] public void Setup() { Services.Reset(); EventHub.Reset(); }
        [TearDown] public void Cleanup() { Services.Reset(); EventHub.Reset(); }
        sealed class Fixture : IDisposable
        {
            readonly List<Object> owned = new();
            readonly BlobAssetReference<BlockDefsBlob> defs;
            readonly BlobAssetReference<BlockModulesBlob> modules;
            readonly BlobAssetReference<OrientationTablesBlob> rotations;
            internal readonly ContentCatalog Catalog;
            internal readonly EditorContext Context;
            internal readonly ProfileBlueprintStore Store;
            internal readonly ShipBlueprintDefinition Content;
            internal readonly string Root;
            internal readonly ushort Core, Hull;
            internal VoxelContent VoxelContent => new() { Blocks = defs, Orientations = rotations };
            internal Fixture()
            {
                Root = Path.Combine(Path.GetTempPath(), "EDT020-" + Guid.NewGuid().ToString("N"));
                var definitions = new List<ContentDefinition>();
                foreach (string path in new[] { "Blocks/block_missing", "Blocks/block_hull", "Blocks/block_core_small", "Items/item_missing" })
                { var asset = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ContentDefinition>("Assets/Game/Content/" + path + ".asset")); owned.Add(asset); definitions.Add(asset); }
                Content = ScriptableObject.CreateInstance<ShipBlueprintDefinition>(); owned.Add(Content);
                using (var edit = new SerializedObject(Content))
                { edit.FindProperty("id").stringValue = "ship_store_fixture"; edit.FindProperty("displayName").stringValue = "Content fixture"; edit.ApplyModifiedPropertiesWithoutUndo(); }
                definitions.Add(Content); var report = new ValidationReport("Blueprint store fixture"); Catalog = ContentCatalog.Build(definitions, report);
                defs = BlockDefsBlobBuilder.Build(Catalog, report, new List<Mesh>(), Allocator.Persistent, out modules);
                Assert.AreEqual(0, report.ErrorCount, report.ToText());
                rotations = OrientationTableGenerator.BuildBlob(OrientationTableGenerator.Generate(), Allocator.Persistent);
                var config = ScriptableObject.CreateInstance<ShipEditorConfig>(); owned.Add(config);
                Context = new EditorContext(config, Catalog, defs, rotations, 16);
                Core = Catalog.GetBlockIndex(Catalog.Get<BlockDefinition>(new ContentId("block_core_small")));
                Hull = Catalog.GetBlockIndex(Catalog.Get<BlockDefinition>(new ContentId("block_hull")));
                var paths = new SavePaths(Root); Directory.CreateDirectory(paths.ProfileDir(new ProfileId("p001")));
                Store = new ProfileBlueprintStore(paths, new ProfileId("p001"), Catalog, Context);
            }
            internal void Edits(VoxelStructure structure, params VoxelEdit[] values)
            {
                using var edits = new NativeArray<VoxelEdit>(values, Allocator.Temp); using var touched = new NativeList<int3>(Allocator.Temp);
                Assert.IsTrue(structure.ApplyEdits(edits, VoxelContent, touched));
            }
            internal void SetContent(VoxelStructure structure)
                => Content.EditorSetPayload(VoxelStructureCodec.Encode(structure, VoxsBlockNames.FromCatalog(Catalog), VoxsEncodeOptions.ForAsset),
                    structure.SolidCellCount, Vector3Int.zero, Vector3Int.one);
            internal EditSession Session(VoxelStructure source) => new(new ShipEditorEntryArgs { Source = source, Options = new EditSessionOptions { Creative = true } }, Context);
            public void Dispose()
            {
                Context.Dispose(); rotations.Dispose(); modules.Dispose(); defs.Dispose();
                foreach (var asset in owned) Object.DestroyImmediate(asset);
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
        }
        [Test] public void SaveLoad_HashEqual()
        {
            using var f = new Fixture(); using var source = new VoxelStructure();
            f.Edits(source, new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Core, Cell = new int3(10, 0, 0), Color = 2 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(11, 0, 0), Color = 4 },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(11, 0, 0), Hp = 71 },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Core, Cell = new int3(10, 0, 0), FunctionHp = 17 });
            f.SetContent(source); using var session = f.Session(source);
            Assert.IsTrue(f.Store.TrySave("My / Ship", source, out string id, out string error), error); Assert.AreEqual("my_ship", id);
            Assert.IsTrue(f.Store.TrySave("My / Ship", source, out string second, out error), error); Assert.AreEqual("my_ship_2", second);
            Assert.IsTrue(File.Exists(Path.Combine(f.Root, "p001/blueprints/my_ship.ssbp")));
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(f.Root, "p001/blueprints"), "*.tmp"));
            var listed = new List<BlueprintInfo>(); f.Store.List(listed); Assert.AreEqual(3, listed.Count);
            Assert.AreEqual(2, listed.Find(row => row.Id == id).BlockCount); Assert.IsFalse(listed.Find(row => row.Id == id).ReadOnly);
            Assert.IsTrue(f.Store.TryLoad(id, out var loaded, out error), error);
            using (loaded) Assert.AreEqual(session.ComputeDraftHash(), new DraftReader(loaded, f.Context).ComputeDraftHash());
            // Profile/path isolation and malformed payloads are save/load edge cases, within this required case.
            var other = new ProfileBlueprintStore(new SavePaths(f.Root), new ProfileId("p002"), f.Catalog, f.Context);
            Assert.IsFalse(other.TryLoad(id, out _, out _)); Assert.IsFalse(f.Store.TryLoad("../my_ship", out _, out _)); Assert.IsFalse(f.Store.TryDelete("../my_ship", out _));
            File.WriteAllBytes(Path.Combine(f.Root, "p001/blueprints/broken.ssbp"), new byte[] { 1, 2 });
            Assert.IsFalse(f.Store.TryLoad("broken", out loaded, out error)); Assert.IsNull(loaded); Assert.IsNotEmpty(error);
            Assert.IsTrue(f.Store.TryLoad(id, out loaded, out error), error); loaded.Dispose();
            Assert.IsTrue(f.Store.TryDelete(id, out error), error); Assert.IsFalse(f.Store.TryLoad(id, out _, out _)); Assert.IsTrue(f.Store.TryLoad(second, out loaded, out error), error); loaded.Dispose();
        }
        [UnityTest] public IEnumerator ContentBlueprint_ReadOnly()
        {
            using var f = new Fixture(); using var source = new VoxelStructure();
            f.Edits(source, new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Core }); f.SetContent(source);
            byte[] before = f.Content.Payload.ToArray(); Assert.IsTrue(f.Store.TryLoad(f.Content.IdString, out var loaded, out var error), error);
            using (loaded) Assert.IsTrue(loaded.IsReadOnly);
            Assert.IsFalse(f.Store.TryDelete(f.Content.IdString, out error)); Assert.IsFalse(f.Store.TrySave(f.Content.IdString, source, out _, out error));
            CollectionAssert.AreEqual(before, f.Content.Payload.ToArray());
            using var session = f.Session(source); using var controller = new ShipEditorController(session, f.Context);
            using var panel = new UITestPanel(1280, 720);
            var config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UIConfig>("Assets/Game/UI/Data/UIConfig.asset")); config.PanelSettings = panel.Settings;
            using var ui = new UIRoot(panel.Document, config, new DialogTestContext.Pause(), new DialogTestContext.Context(), new NullUISoundSink(), new UnityFrameClock());
            try
            {
                var model = new BlueprintBrowserViewModel(); BlueprintBrowserView view = null; string status = null;
                var actions = new BlueprintBrowserActions(f.Store, session, f.Context, controller, model, () => ui.Navigator.Pop(view), text => status = text);
                view = new BlueprintBrowserView(model, actions); ui.Register(view); actions.Refresh(); actions.Select(0); ui.Navigator.Push(view);
                yield return null; // Only the real ListView's initial layout needs a frame.
                Assert.IsNotNull(view.Root.Q<ListView>("blueprint-browser-list")); Assert.IsFalse(view.Root.Q<Button>("blueprint-browser-delete").enabledInHierarchy);
                Assert.IsTrue(view.Root.Q<Button>("blueprint-browser-stamp").enabledInHierarchy);
                actions.Delete(); CollectionAssert.AreEqual(before, f.Content.Payload.ToArray());
                model.CanDelete = true; Assert.IsFalse(view.Root.Q<Button>("blueprint-browser-delete").enabledInHierarchy, "Read-only row wins over a stale delete flag.");
                Assert.IsNotNull(view.Root.Q("blueprint-browser-row-0"));
                model.Rows[0].Name = "Current content name"; Assert.AreEqual("Current content name", view.Root.Q<Label>("blueprint-browser-row-0-name").text);
                actions.LoadStamp(); Assert.AreEqual(ToolId.Paste, controller.ActiveTool); Assert.IsFalse(view.IsOpen); Assert.IsNull(status);
                model.CanLoad = false; Assert.IsTrue(view.Root.Q<Button>("blueprint-browser-stamp").enabledSelf, "Closed view releases model notifications.");
                ui.Navigator.Push(view); Assert.IsFalse(view.Root.Q<Button>("blueprint-browser-stamp").enabledSelf);
                ui.Navigator.Pop(view);
            }
            finally { Object.DestroyImmediate(config); }
        }
        [Test] public void ReplaceAll_IsOneUndoStep()
        {
            using var f = new Fixture(); using var old = new VoxelStructure(); using var replacement = new VoxelStructure();
            f.Edits(old, new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Core, Cell = new int3(10, 0, 0), Color = 2 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(11, 0, 0) },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(11, 0, 0), Hp = 37 },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Core, Cell = new int3(10, 0, 0), FunctionHp = 19 });
            f.Edits(replacement, new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Core, Cell = new int3(3, 0, 0), Color = 5 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(3, 0, 1), Color = 7 });
            f.SetContent(replacement); using var session = f.Session(old); using var controller = new ShipEditorController(session, f.Context);
            var model = new BlueprintBrowserViewModel(); int closed = 0; string status = null;
            var actions = new BlueprintBrowserActions(f.Store, session, f.Context, controller, model, () => closed++, text => status = text);
            actions.Refresh(); actions.Select(0); ulong before = session.ComputeDraftHash(); actions.LoadReplace();
            Assert.IsNull(status); Assert.AreEqual(1, closed); Assert.AreEqual(1, session.History.Count); Assert.AreEqual(4, session.History[0].Ops.Length);
            Assert.IsTrue(session.Draft.TryGetBlockAt(new int3(10, 0, 0), out var core, out _)); Assert.AreEqual(5, core.Color);
            Assert.IsTrue(session.Draft.TryGetBlockAt(new int3(10, 0, 1), out var hull, out _)); Assert.AreEqual(7, hull.Color); Assert.AreEqual(255, session.Draft.GetCell(hull.Origin).Hp);
            ulong replaced = session.ComputeDraftHash(); Assert.IsTrue(session.Undo()); Assert.AreEqual(before, session.ComputeDraftHash());
            Assert.IsTrue(session.Redo()); Assert.AreEqual(replaced, session.ComputeDraftHash());
            Assert.IsTrue(session.Undo()); Assert.AreEqual(before, session.ComputeDraftHash());
            // A bad far-away block must reject the entire replacement and retain the damaged draft/history cursor.
            using var bad = replacement.Clone(); f.Edits(bad, new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(700, 0, 0) }); f.SetContent(bad);
            status = null; actions.LoadReplace(); Assert.IsNotNull(status); Assert.AreEqual(before, session.ComputeDraftHash()); Assert.AreEqual(0, session.History.Cursor); Assert.AreEqual(1, closed);
            // With no draft core, the source core is aligned to the grid origin.
            using var empty = new VoxelStructure(); using var emptySession = f.Session(empty); using var emptyController = new ShipEditorController(emptySession, f.Context);
            f.SetContent(replacement); var emptyModel = new BlueprintBrowserViewModel();
            var intoEmpty = new BlueprintBrowserActions(f.Store, emptySession, f.Context, emptyController, emptyModel, null, null);
            intoEmpty.Refresh(); intoEmpty.Select(0); intoEmpty.LoadReplace(); Assert.IsFalse(emptySession.Draft.GetCell(int3.zero).IsAir); Assert.AreEqual(1, emptySession.History.Count);
        }
    }
}
