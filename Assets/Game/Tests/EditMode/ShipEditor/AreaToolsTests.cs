using System.Collections.Generic;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.ShipEditor.Tools;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class AreaToolsTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static EditSession Session(DraftAdapterTests.Fixture f) => new(new ShipEditorEntryArgs {
            Source = f.Draft, Options = new EditSessionOptions { Creative = true } }, f.Context);
        static Ray Ray(int x = 0, int z = 0) => new(new Vector3(x + .5f, 10, z + .5f), Vector3.down);
        static void Pick(ToolContext context, int3 cell)
        {
            context.GridRay = new Ray(new Vector3(cell.x + .5f, cell.y + 10, cell.z + .5f), Vector3.down);
            context.Hover = DraftPicker.Raycast(context.Session.Draft, context.Content, context.GridRay, true, cell.y);
        }
        [Test] public void Line_DominantAxis()
        {
            using var f = new DraftAdapterTests.Fixture();
            int3[] ends = { new(3, 2, 1), new(3, 3, 3), new(0, 3, 3), new(0, 4, 1), new(300, 0, 0) };
            int[] axes = { 0, 0, 2, 1, 0 }, counts = { 4, 4, 4, 5, 256 };
            for (int test = 0; test < ends.Length; test++)
            {
                using var session = Session(f); using var context = new ToolContext(session, f.Context); using var tool = new LineTool();
                context.SelectedBlock = f.Hull; Pick(context, int3.zero);
                tool.Tick(context, new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true });
                Pick(context, ends[test]); tool.Tick(context, new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true });
                Assert.AreEqual(counts[test], session.Draft.SolidCellCount); Assert.AreEqual(1, session.History.Count);
                for (int i = 0; i < counts[test]; i++) { int3 cell = default; cell[axes[test]] = i; Assert.IsFalse(session.Draft.GetCell(cell).IsAir); }
                Assert.IsTrue(session.Undo()); Assert.AreEqual(0, session.Draft.SolidCellCount);
            }
            using var mirrored = Session(f); using var shared = new ToolContext(mirrored, f.Context); using var line = new LineTool();
            shared.SelectedBlock = f.Hull; shared.Symmetry = new SymmetrySettings { MirrorX = true, PlaneX2 = 1 };
            Pick(shared, int3.zero); line.Tick(shared, new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true });
            Pick(shared, new int3(3, 0, 0)); line.Tick(shared, new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true });
            Assert.AreEqual(7, mirrored.Draft.SolidCellCount); Assert.AreEqual(1, mirrored.History.Count);
            Assert.IsTrue(mirrored.Undo()); Assert.AreEqual(0, mirrored.Draft.SolidCellCount);
        }
        [Test] public void Box_Solid_Hollow_Frame_Counts()
        {
            using var f = new DraftAdapterTests.Fixture(); int[] counts = { 64, 64, 40 };
            for (int mode = 0; mode < 3; mode++)
            {
                using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
                controller.SelectBlock(f.Hull); controller.SelectTool(ToolId.Box);
                for (int i = 0; i < mode; i++) controller.CycleBoxMode();
                controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
                controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true, ToolWheel = 3 }, Ray(7, 1));
                controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true }, Ray(7, 1));
                Assert.AreEqual(counts[mode], session.Draft.SolidCellCount); Assert.AreEqual(1, session.History.Count);
                for (int x = 0; x < 8; x++) for (int y = 0; y < 4; y++) for (int z = 0; z < 2; z++)
                {
                    bool expected = mode < 2 || x == 0 || x == 7 || y == 0 || y == 3;
                    Assert.AreEqual(expected, !session.Draft.GetCell(new int3(x, y, z)).IsAir);
                }
                Assert.IsTrue(session.Undo()); Assert.AreEqual(0, session.Draft.SolidCellCount);
            }
            // A non-degenerate shell leaves its centre empty and budgets only the 26 generated cells.
            f.Context.Config.MaxBatchCells = 26;
            using var hollow = Session(f); using var box = new ShipEditorController(hollow, f.Context);
            box.SelectBlock(f.Hull); box.SelectTool(ToolId.Box); box.CycleBoxMode();
            box.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
            box.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true, ToolWheel = 2 }, Ray(2, 2));
            box.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true }, Ray(2, 2));
            Assert.AreEqual(26, hollow.Draft.SolidCellCount); Assert.IsTrue(hollow.Draft.GetCell(new int3(1)).IsAir);
        }
        [Test] public void Box_WheelHeight()
        {
            using var f = new DraftAdapterTests.Fixture(); f.Context.Config.PreviewOutlineThreshold = 8;
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectBlock(f.Hull); controller.SelectTool(ToolId.Box);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
            Assert.IsTrue(controller.ConsumesWheel);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true, ToolWheel = 2 }, Ray(2, 2));
            Assert.IsTrue(controller.Preview.HasBounds); Assert.AreEqual(0, controller.Preview.Records.Length);
            Assert.AreEqual(new int3(2, 2, 2), controller.Preview.BoundsMax);
            Assert.AreEqual("3 × 3 × 3 = 27", controller.Preview.StatusText); Assert.AreEqual(0, session.Draft.SolidCellCount);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true, ToolWheel = 1000 }, Ray(2, 2));
            Assert.AreEqual(255, controller.Preview.BoundsMax.y);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true, ToolWheel = -1000 }, Ray(2, 2));
            Assert.AreEqual(0, controller.Preview.BoundsMax.y);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true, ToolWheel = 2 }, Ray(2, 2));
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true }, Ray(2, 2));
            Assert.AreEqual(27, session.Draft.SolidCellCount); Assert.IsFalse(controller.ConsumesWheel);
            Assert.IsTrue(session.Undo()); f.Context.Config.MaxBatchCells = 8;
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryHeld = true, ToolWheel = 2 }, Ray(2, 2));
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryReleased = true }, Ray(2, 2));
            Assert.AreEqual(0, session.Draft.SolidCellCount); Assert.AreEqual(PlacementError.OverBudget, controller.LastError);
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
            controller.Tick(new EditorInputFrame { PrimaryReleased = true }, Ray());
            Assert.IsFalse(controller.ConsumesWheel); Assert.AreEqual(0, session.Draft.SolidCellCount);
        }
        [Test] public void MultiCell_Rejected()
        {
            using var f = new DraftAdapterTests.Fixture();
            foreach (var tool in new[] { ToolId.Line, ToolId.Box, ToolId.Fill })
            {
                using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
                controller.SelectBlock(f.Multi); controller.SelectTool(tool);
                controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray());
                Assert.AreEqual(PlacementError.MultiCellNotAllowed, controller.LastError);
                Assert.AreEqual(0, session.History.Count); Assert.AreEqual(0, session.Draft.SolidCellCount);
            }
        }
        static void SeedRegion(DraftAdapterTests.Fixture f, bool open = false)
        {
            var edits = new List<VoxelEdit>();
            for (int x = 0; x < 5; x++) for (int z = 0; z < 5; z++)
                if ((x == 0 || x == 4 || z == 0 || z == 4) && !(open && x == 2 && z == 4))
                    edits.Add(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(x, 0, z) });
            edits.Add(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(2, -1, 2) });
            f.Edits(edits.ToArray());
        }
        [Test] public void Fill_Enclosed_FillsRegion()
        {
            using var f = new DraftAdapterTests.Fixture(); SeedRegion(f);
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectBlock(f.Hull); controller.SelectTool(ToolId.Fill); ulong before = session.ComputeDraftHash();
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray(2, 2));
            Assert.AreEqual(26, session.Draft.SolidCellCount); Assert.AreEqual(PlacementError.None, controller.LastError);
            for (int x = 1; x <= 3; x++) for (int z = 1; z <= 3; z++) Assert.IsFalse(session.Draft.GetCell(new int3(x, 0, z)).IsAir);
            Assert.AreEqual(1, session.History.Count); Assert.IsTrue(session.Undo()); Assert.AreEqual(before, session.ComputeDraftHash());
        }
        [Test] public void Fill_Open_Aborts()
        {
            using var f = new DraftAdapterTests.Fixture(); SeedRegion(f, open: true);
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectBlock(f.Hull); controller.SelectTool(ToolId.Fill); ulong before = session.ComputeDraftHash();
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray(2, 2));
            Assert.AreEqual("Region not enclosed", controller.Preview.StatusText);
            Assert.AreEqual(0, session.History.Count); Assert.AreEqual(before, session.ComputeDraftHash());
        }
        [Test] public void Fill_TooLarge_Aborts()
        {
            using var f = new DraftAdapterTests.Fixture(); SeedRegion(f); f.Context.Config.FillMaxCells = 4;
            using var session = Session(f); using var controller = new ShipEditorController(session, f.Context);
            controller.SelectBlock(f.Hull); controller.SelectTool(ToolId.Fill); ulong before = session.ComputeDraftHash();
            controller.Tick(new EditorInputFrame { PointerAllowed = true, PrimaryPressed = true }, Ray(2, 2));
            Assert.AreEqual("Region too large", controller.Preview.StatusText); Assert.AreEqual(PlacementError.OverBudget, controller.LastError);
            Assert.AreEqual(0, session.History.Count); Assert.AreEqual(before, session.ComputeDraftHash());
        }
    }
}
