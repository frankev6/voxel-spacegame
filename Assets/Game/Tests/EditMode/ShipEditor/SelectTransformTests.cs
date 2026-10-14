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
    public sealed class SelectTransformTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static EditSession Session(DraftAdapterTests.Fixture f) => new(new ShipEditorEntryArgs {
            Source = f.Draft, Options = new EditSessionOptions { Creative = true } }, f.Context);
        static Ray Ray(float x = 0) => new(new Vector3(x + .5f, 10, .5f), Vector3.down);
        static void Action(ShipEditorController controller, EditorActions action)
            => controller.Tick(new EditorInputFrame { Actions = action }, Ray());
        static void Click(ShipEditorController controller, float x, bool ctrl = false)
        {
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true, Ctrl = ctrl }, Ray(x));
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true, Ctrl = ctrl }, Ray(x));
        }
        [Test] public void RotateFourTimes_Identity()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Cell = new int3(3, 0, 2), Color = 4 },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(4, 0, 2), Hp = 39 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            Action(controller, EditorActions.SelectAll); ulong before = session.ComputeDraftHash();
            for (int i = 0; i < 4; i++)
            { Action(controller, EditorActions.RotateYaw); Assert.AreEqual(PlacementError.None, controller.LastError); }
            Assert.AreEqual(before, session.ComputeDraftHash()); Assert.AreEqual(4, session.History.Count);
        }
        [Test] public void MirrorTwice_Identity()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Prop, Color = 3 },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(1, 0, 0), Hp = 22 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            Action(controller, EditorActions.SelectAll); ulong before = session.ComputeDraftHash();
            Action(controller, EditorActions.MirrorSelection); Assert.AreEqual(PlacementError.None, controller.LastError);
            Action(controller, EditorActions.MirrorSelection); Assert.AreEqual(PlacementError.None, controller.LastError);
            Assert.AreEqual(before, session.ComputeDraftHash()); Assert.AreEqual(1, controller.Selection.Count);
        }
        [Test] public void MoveDamaged_KeepsDamage_NoBom()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi },
                new VoxelEdit { Kind = VoxelEditKind.ClearCell },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(1, 0, 0), Hp = 43 },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Multi, FunctionHp = 0 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            var reader = new DraftReader(session.Draft, f.Context);
            Assert.IsTrue(reader.TryGetBlockAt(new int3(1, 0, 0), out var original));
            using var expected = new NativeList<byte>(Allocator.Temp); reader.CaptureDamage(original, expected);
            controller.SelectTool(ToolId.Select); Click(controller, 1);
            Assert.IsTrue(controller.Selection.Contains(int3.zero));
            Action(controller, EditorActions.MoveRight); Assert.AreEqual(PlacementError.None, controller.LastError);
            Assert.IsTrue(reader.TryGetBlockAt(new int3(2, 0, 0), out var moved)); Assert.AreEqual(new int3(1, 0, 0), moved.Origin);
            using var actual = new NativeList<byte>(Allocator.Temp); reader.CaptureDamage(moved, actual);
            CollectionAssert.AreEqual(expected.AsArray().ToArray(), actual.AsArray().ToArray());
            Assert.AreEqual(0, session.GetBom().Lines.Count); Assert.IsTrue(controller.Selection.Contains(moved.Origin));
            Assert.IsTrue(reader.TryGetBlockAt(new int3(1, 0, 0), out var movedHull)); Assert.AreEqual(f.Hull, movedHull.Block);
            Assert.IsTrue(session.Undo()); Assert.IsTrue(controller.Selection.Contains(int3.zero));
            Assert.IsFalse(controller.Selection.Contains(new int3(1, 0, 0)));
            Assert.IsTrue(session.Redo()); Assert.IsTrue(controller.Selection.Contains(new int3(1, 0, 0)));
            Assert.IsFalse(controller.Selection.Contains(int3.zero));
            Assert.IsTrue(session.Undo());
            Action(controller, EditorActions.MoveRight); Assert.AreEqual(PlacementError.None, controller.LastError);

        }
        [Test] public void Move_IntoOccupied_RejectedAll()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(1, 0, 0) });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectTool(ToolId.Select); Click(controller, 0); ulong before = session.ComputeDraftHash();
            Action(controller, EditorActions.MoveRight);
            Assert.AreEqual(PlacementError.Occupied, controller.LastError); Assert.AreEqual(before, session.ComputeDraftHash());
            Assert.IsTrue(controller.Selection.Contains(int3.zero)); Assert.AreEqual(0, session.History.Count);
        }
        [Test] public void Replace_SelectedOfType()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Color = 2 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(1, 0, 0), Color = 3 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Single, Cell = new int3(2, 0, 0), Color = 4 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectTool(ToolId.Select); Click(controller, 0); Click(controller, 1, true); Click(controller, 2, true);
            controller.SelectBlock(f.Single); controller.SelectTool(ToolId.Replace); Click(controller, 0);
            var reader = new DraftReader(session.Draft, f.Context);
            for (int x = 0; x < 3; x++)
            { Assert.IsTrue(reader.TryGetBlockAt(new int3(x, 0, 0), out var record)); Assert.AreEqual(f.Single, record.Block); Assert.AreEqual(x + 2, record.Color); }
            Assert.AreEqual(1, session.History.Count); Assert.AreEqual(4, session.History[0].Ops.Length);
        }
        [Test] public void Replace_IncompatibleFootprint_Skipped()
        {
            using var f = new DraftAdapterTests.Fixture(); f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectTool(ToolId.Replace); controller.SelectBlock(f.Hull); ulong before = session.ComputeDraftHash();
            Click(controller, 0); Assert.AreEqual(before, session.ComputeDraftHash()); Assert.AreEqual(0, session.History.Count);
        }
    }
}
