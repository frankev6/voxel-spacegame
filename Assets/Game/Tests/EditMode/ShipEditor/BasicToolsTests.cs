using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class BasicToolsTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static EditSession Session(DraftAdapterTests.Fixture f) => new(new ShipEditorEntryArgs {
            Source = f.Draft, Options = new EditSessionOptions { Creative = true } }, f.Context);
        static Ray Ray(float x = 0, float z = 0) => new(new Vector3(x + .5f, 10, z + .5f), Vector3.down);
        [Test] public void PlaceStroke_OneUndoStep()
        {
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f);
            using var controller = new ShipEditorController(session, f.Context); controller.SelectBlock(f.Hull);
            ulong original = session.ComputeDraftHash();
            for (int i = 0; i < 50; i++) controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = i == 0, PrimaryHeld = true }, Ray(i));
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true }, Ray(49));
            Assert.AreEqual(50, session.GetStats(false).Blocks); Assert.AreEqual(1, session.History.Count);
            Assert.IsTrue(session.Undo()); Assert.AreEqual(original, session.ComputeDraftHash());
        }
        [Test] public void PlaceHeldStill_PlacesOneBlock()
        {
            // QA-001: re-picking each frame placed on the stroke's own block and grew a column toward the camera.
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f);
            using var controller = new ShipEditorController(session, f.Context); controller.SelectBlock(f.Hull);
            for (int i = 0; i < 20; i++) controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = i == 0, PrimaryHeld = true }, Ray());
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true }, Ray());
            Assert.AreEqual(1, session.GetStats(false).Blocks); Assert.AreEqual(1, session.History.Count);
        }
        [Test] public void RemoveHeldStill_RemovesOnlyTheFirstLayer()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(0, 0, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(0, 1, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(0, 2, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(1, 2, 0) });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectTool(ToolId.Remove);
            for (int i = 0; i < 20; i++) controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = i == 0, PrimaryHeld = true }, Ray());
            Assert.AreEqual(3, session.GetStats(false).Blocks, "holding still removes the hovered block only, never drilling");
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true }, Ray(1));
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true }, Ray(1));
            Assert.AreEqual(2, session.GetStats(false).Blocks, "dragging continues across the same layer");
            Assert.AreEqual(1, session.History.Count);
        }
        [Test] public void PointerBlocked_NoEdit()
        {
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f);
            using var controller = new ShipEditorController(session, f.Context); controller.SelectBlock(f.Hull);
            controller.Tick(new EditorInputFrame { PointerAllowed = true }, Ray());
            Assert.AreEqual(DraftPicker.HitKind.Plane, controller.Hover.Kind);
            controller.Tick(new EditorInputFrame { PrimaryPressed = true, PrimaryHeld = true, SecondaryPressed = true }, Ray());
            Assert.AreEqual(DraftPicker.HitKind.None, controller.Hover.Kind); Assert.AreEqual(0, session.History.Count);
            Assert.AreEqual(0, controller.Preview.Records.Length);
            int version = controller.Preview.Version; controller.Tick(default, Ray()); Assert.AreEqual(version, controller.Preview.Version);
        }
        [Test] public void SecondaryClick_RemovesWithSymmetry()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(1, 0, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(-1, 0, 0) });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.ToggleMirror(0); controller.Tick(new EditorInputFrame { PointerAllowed = true, SecondaryPressed = true }, Ray(1));
            Assert.AreEqual(0, session.GetStats(false).Blocks); Assert.AreEqual(1, session.History.Count);
            session.Undo(); Assert.AreEqual(2, session.GetStats(false).Blocks);
        }
        [Test] public void Rotate_SkipsDisallowed()
        {
            using var f = new DraftAdapterTests.Fixture(); ref var tables = ref f.Context.Orientations.Value;
            byte halfTurn = tables.AxisTurn(1, 2);
            f.Context.Definitions.Value.Blocks[f.Hull].AllowedOrientations = 1u | (1u << halfTurn);
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context); controller.SelectBlock(f.Hull);
            controller.Tick(new EditorInputFrame { Actions = EditorActions.RotateYaw }, Ray()); Assert.AreEqual(halfTurn, controller.Orient);
            controller.Tick(new EditorInputFrame { Actions = EditorActions.RotateYaw, Shift = true }, Ray()); Assert.AreEqual(0, controller.Orient);
        }
        [Test] public void Paint_KeepsDamage()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi },
                new VoxelEdit { Kind = VoxelEditKind.ClearCell },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(1, 0, 0), Hp = 43 },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Multi, FunctionHp = 0 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            var reader = new DraftReader(session.Draft, f.Context); Assert.IsTrue(reader.TryGetBlockAt(new int3(1, 0, 0), out var before));
            using var expected = new NativeList<byte>(Allocator.Temp); reader.CaptureDamage(before, expected);
            controller.SelectTool(ToolId.Paint); controller.SelectColor(9);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray(1));
            Assert.IsTrue(reader.TryGetBlockAt(new int3(1, 0, 0), out var after)); Assert.AreEqual(9, after.Color);
            using var actual = new NativeList<byte>(Allocator.Temp); reader.CaptureDamage(after, actual);
            CollectionAssert.AreEqual(expected.AsArray().ToArray(), actual.AsArray().ToArray());
            Assert.IsTrue(session.Undo()); Assert.IsTrue(reader.TryGetBlockAt(new int3(1, 0, 0), out var restored)); Assert.AreEqual(before.Color, restored.Color);
        }
        [Test] public void Eyedropper_CopiesTypeOrientColor()
        {
            using var f = new DraftAdapterTests.Fixture(); f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Single, Orient = 5, Color = 7 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectTool(ToolId.Eyedropper); controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
            Assert.AreEqual(f.Single, controller.SelectedBlock); Assert.AreEqual(5, controller.Orient); Assert.AreEqual(7, controller.Color);
            Assert.AreEqual(0, session.History.Count);
        }
        [Test] public void CancelChain_Order()
        {
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f); int leave = 0;
            using var controller = new ShipEditorController(session, f.Context, () => leave++); controller.SelectBlock(f.Hull);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
            controller.Selection.Add(int3.zero); Assert.IsTrue(controller.Cancel()); Assert.AreEqual(1, controller.Selection.Count); Assert.AreEqual(0, leave);
            controller.SelectTool(ToolId.Paste);
            Assert.IsTrue(controller.Cancel()); Assert.AreEqual(ToolId.Place, controller.ActiveTool); Assert.AreEqual(1, controller.Selection.Count);
            Assert.IsTrue(controller.Cancel()); Assert.AreEqual(0, controller.Selection.Count); Assert.AreEqual(0, leave);
            Assert.IsTrue(controller.Cancel()); Assert.AreEqual(1, leave);
        }
    }
}
