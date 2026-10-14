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
    public sealed class ClipboardTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static EditSession Session(DraftAdapterTests.Fixture f, VoxelStructure source = null) => new(new ShipEditorEntryArgs {
            Source = source ?? f.Draft, Options = new EditSessionOptions { Creative = true } }, f.Context);
        static Ray Front(float x, float y = 0) => new(new Vector3(x + .5f, y + .5f, -10), Vector3.forward);
        static Ray Down(float x, float z = 0) => new(new Vector3(x + .5f, 10, z + .5f), Vector3.down);
        static void Action(ShipEditorController controller, EditorActions actions, bool reverse = false)
            => controller.Tick(new EditorInputFrame { Actions = actions, Shift = reverse }, Front(0));
        static void Hover(ShipEditorController controller, Ray ray, bool click = false)
            => controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = click }, ray);
        static BlockRecord At(EditSession session, DraftAdapterTests.Fixture f, int3 cell)
        {
            Assert.IsTrue(new DraftReader(session.Draft, f.Context).TryGetBlockAt(cell, out var record), cell.ToString());
            return record;
        }
        [Test] public void CopyPaste_RelativeLayout()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(3, 0, 2), Color = 2 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Cell = new int3(4, 0, 2), Color = 7 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            Action(controller, EditorActions.SelectAll); Action(controller, EditorActions.Copy); Action(controller, EditorActions.Paste);
            Assert.AreEqual(ToolId.Paste, controller.ActiveTool);
            controller.SelectBlock(f.Single); controller.SelectColor(13); // Palette state never repaints/replaces clipboard records.
            ulong before = session.ComputeDraftHash(); Hover(controller, Front(3));
            Assert.AreEqual(PlacementError.None, controller.LastError); Assert.AreEqual(before, session.ComputeDraftHash());
            Assert.AreEqual(new int3(3, 0, 1), controller.Preview.Records[0].Origin);
            Assert.AreEqual(new int3(4, 0, 1), controller.Preview.Records[1].Origin);
            int previewVersion = controller.Preview.Version; Hover(controller, Front(3)); Assert.AreEqual(previewVersion, controller.Preview.Version);
            Hover(controller, Front(4)); Assert.AreEqual(new int3(4, 0, 1), controller.Preview.Records[0].Origin);
            Hover(controller, Front(3), true);
            var hull = At(session, f, new int3(3, 0, 1)); var multi = At(session, f, new int3(4, 0, 1));
            Assert.AreEqual(f.Hull, hull.Block); Assert.AreEqual(2, hull.Color); Assert.AreEqual(0, hull.Orient);
            Assert.AreEqual(f.Multi, multi.Block); Assert.AreEqual(7, multi.Color); Assert.AreEqual(0, multi.Orient);
            Assert.AreEqual(new int3(1, 0, 0), multi.Origin - hull.Origin);
            Assert.AreEqual(1, session.History.Count); Assert.AreEqual(ToolId.Paste, session.History[0].Tool);
            ulong pasted = session.ComputeDraftHash(); Assert.IsTrue(session.Undo()); Assert.AreEqual(before, session.ComputeDraftHash());
            Assert.IsTrue(session.Redo()); Assert.AreEqual(pasted, session.ComputeDraftHash());
            Assert.IsTrue(controller.Cancel()); Assert.AreEqual(ToolId.Select, controller.ActiveTool); Assert.AreEqual(2, controller.Selection.Count);
        }
        [Test] public void Paste_IsPristine()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Color = 4 },
                new VoxelEdit { Kind = VoxelEditKind.ClearCell },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(1, 0, 0), Hp = 41 },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Multi, FunctionHp = 0 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            Action(controller, EditorActions.SelectAll); Action(controller, EditorActions.Copy); Action(controller, EditorActions.Paste);
            Hover(controller, Front(1), true); Assert.AreEqual(1, session.History.Count);
            var pasted = At(session, f, new int3(1, 0, -1)); Assert.AreEqual(new int3(1, 0, -1), pasted.Origin);
            Assert.IsTrue(session.Draft.TryGetBlockAt(pasted.Origin, out _, out int instance));
            Assert.AreEqual(f.Context.Definitions.Value.Blocks[f.Multi].FunctionHp, session.Draft.Data.Instances[instance].FunctionHp);
            foreach (var cell in f.Context.Footprints.Get(pasted.Block, pasted.Orient).Cells)
                Assert.AreEqual(255, session.Draft.GetCell(pasted.Origin + cell).Hp);
            Assert.IsTrue(session.Draft.GetCell(int3.zero).IsAir); Assert.AreEqual(41, session.Draft.GetCell(new int3(1, 0, 0)).Hp);
            // BeginStamp copies all records and owns no reference to the caller's disposable structure.
            controller.Cancel(); using (var stamp = f.Draft.Clone()) controller.BeginStamp(stamp);
            Assert.AreEqual(ToolId.Paste, controller.ActiveTool); Hover(controller, Front(1), true);
            var stamped = At(session, f, new int3(1, 0, -2));
            using var damage = new NativeList<byte>(Allocator.Temp);
            Assert.AreEqual(-1, new DraftReader(session.Draft, f.Context).CaptureDamage(stamped, damage));
            Assert.AreEqual(2, session.History.Count); Assert.AreEqual(0, session.History[0].Damage.Length); Assert.AreEqual(0, session.History[1].Damage.Length);
        }
        [Test] public void CutUndo_RestoresWithDamage()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Color = 6 },
                new VoxelEdit { Kind = VoxelEditKind.ClearCell },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(1, 0, 0), Hp = 39 },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Multi, FunctionHp = 0 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(2, 0, 0), Color = 3 });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            ulong before = session.ComputeDraftHash(); var damaged = At(session, f, new int3(1, 0, 0));
            using var expected = new NativeList<byte>(Allocator.Temp); new DraftReader(session.Draft, f.Context).CaptureDamage(damaged, expected);
            Action(controller, EditorActions.SelectAll); Action(controller, EditorActions.Cut);
            Assert.AreEqual(0, session.Draft.SolidCellCount); Assert.AreEqual(0, controller.Selection.Count);
            Assert.AreEqual(1, session.History.Count); Assert.AreEqual("Cut selection", session.History[0].Label);
            Assert.AreEqual(2, session.History[0].Ops.Length); Assert.Greater(session.History[0].Damage.Length, 0);
            Action(controller, EditorActions.Undo); Assert.AreEqual(before, session.ComputeDraftHash());
            using var restored = new NativeList<byte>(Allocator.Temp);
            new DraftReader(session.Draft, f.Context).CaptureDamage(At(session, f, new int3(1, 0, 0)), restored);
            CollectionAssert.AreEqual(expected.AsArray().ToArray(), restored.AsArray().ToArray());
            Action(controller, EditorActions.Paste); Hover(controller, Front(1)); Assert.AreEqual(2, controller.Preview.Records.Length);
            foreach (var record in controller.Preview.Records) Assert.AreEqual(-1, record.DamageOffset);
        }
        [Test] public void Paste_Rotated()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Cell = new int3(3, 0, 2), Color = 4 },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(5, 0, 2), Color = 7 });
            using var empty = new VoxelStructure(); using var session = Session(f, empty); using var controller = new ShipEditorController(session, f.Context);
            using (var stamp = f.Draft.Clone()) controller.BeginStamp(stamp);
            var ray = Down(20, 20); Hover(controller, ray); var initial = controller.Preview.Records.ToArray();
            for (int i = 0; i < 4; i++) Action(controller, EditorActions.RotateYaw);
            Hover(controller, ray); CollectionAssert.AreEqual(initial, controller.Preview.Records.ToArray());
            Action(controller, EditorActions.RotateYaw); Action(controller, EditorActions.RotateYaw, true);
            Hover(controller, ray); CollectionAssert.AreEqual(initial, controller.Preview.Records.ToArray());
            Action(controller, EditorActions.RotateYaw); Hover(controller, ray);
            Assert.AreEqual(PlacementError.None, controller.LastError); Assert.AreEqual(0, session.History.Count);
            Hover(controller, ray, true); Assert.AreEqual(1, session.History.Count);
            var anchor = new int3(20, 0, 20); var multi = At(session, f, anchor + new int3(0, 0, 2)); var hull = At(session, f, anchor);
            Assert.AreEqual(anchor + new int3(0, 0, 2), multi.Origin); Assert.AreEqual(f.Multi, multi.Block); Assert.AreEqual(4, multi.Color);
            Assert.AreEqual(f.Hull, hull.Block); Assert.AreEqual(7, hull.Color);
            byte yaw = f.Context.Orientations.Value.AxisTurn(1, 1); Assert.AreEqual(yaw, multi.Orient); Assert.AreEqual(yaw, hull.Orient);
            Assert.AreEqual(5, session.Draft.SolidCellCount); Assert.AreEqual(0, session.History[0].Damage.Length);
        }
        [Test] public void Paste_Overlap_RejectedAll()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(1, 0, 0) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(1, 0, -1) },
                new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(-1, 1, 0) });
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.Selection.Add(int3.zero); controller.Selection.Add(new int3(1, 0, 0)); Action(controller, EditorActions.Copy); Action(controller, EditorActions.Paste);
            ulong before = session.ComputeDraftHash(); Hover(controller, Front(0)); Assert.AreEqual(PlacementError.Occupied, controller.LastError);
            Hover(controller, Front(0), true); Assert.AreEqual(before, session.ComputeDraftHash()); Assert.AreEqual(0, session.History.Count);
            Assert.IsTrue(session.Draft.GetCell(new int3(0, 0, -1)).IsAir);
            controller.ToggleMirror(0); Hover(controller, Down(0)); Assert.AreEqual(PlacementError.Occupied, controller.LastError);
            Hover(controller, Down(0), true); Assert.AreEqual(before, session.ComputeDraftHash()); Assert.AreEqual(0, session.History.Count);
            Assert.IsTrue(session.Draft.GetCell(new int3(0, 1, 0)).IsAir);
            // Identity on the centre plane is deduplicated; all other mirrored blocks join this one command.
            var back = new Ray(new Vector3(.5f, .5f, 10), Vector3.back); Hover(controller, back);
            Assert.AreEqual(3, controller.Preview.Records.Length); Assert.AreEqual(PlacementError.None, controller.LastError);
            Hover(controller, back, true); Assert.AreEqual(1, session.History.Count); Assert.AreEqual(3, session.History[0].Ops.Length);
            Assert.IsFalse(session.Draft.GetCell(new int3(-1, 0, 1)).IsAir); Assert.IsTrue(session.Undo()); Assert.AreEqual(before, session.ComputeDraftHash());
        }
    }
}
