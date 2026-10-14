using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using static Game.Tests.EditMode.Data.TestContentFactory;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class PlacementValidatorTests
    {
        [TearDown] public void Cleanup() => DestroyAll();
        static DraftAdapterTests.Fixture Fixture() => new(defs => {
            defs.Add(Block("block_reservation", BlockKind.Functional, edit: so => Set(so, "clearance", new[] { new Vector3Int(1, 0, 0) })));
            defs.Add(Block("block_no_attach", BlockKind.Structural, edit: so => Set(so, "attachFaces", 0)));
            defs.Add(Block("block_upright", BlockKind.Structural, edit: so => Set(so, "allowedOrientations.Bits", 1u)));
            defs.Add(Block("block_core", BlockKind.Functional, modules: new BlockModule[] { new CoreModule { Params = new CoreParams { MaxCells = 100, MaxExtentCells = 100 } } }));
        });
        static ushort Id(DraftAdapterTests.Fixture f, string id) => f.Catalog.GetBlockIndex(f.Catalog.Get<BlockDefinition>(new ContentId(id)));
        static BlockRecord Record(ushort block, int x = 0, byte orient = 0)
            => new() { Block = block, Origin = new int3(x, 0, 0), Orient = orient, DamageOffset = -1 };
        static void Place(DraftAdapterTests.Fixture f, ushort block, int x = 0)
            => f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = block, Cell = new int3(x, 0, 0) });
        static EditSession Session(DraftAdapterTests.Fixture f) => new(new ShipEditorEntryArgs { Source = f.Draft }, f.Context);
        static void Add(EditBatch batch, BlockRecord record) => batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = record });
        static void Remove(EditBatch batch, BlockRecord record) => batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record });
        static void Refuses(EditSession session, EditBatch batch, PlacementError expected)
        {
            var report = new ApplyReport(); ulong hash = session.ComputeDraftHash(); int history = session.History.Count;
            Assert.IsFalse(session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "invalid", report));
            Assert.AreEqual(expected, report.GroupErrors[report.GroupErrors.Count - 1]); Assert.AreEqual(0, report.AcceptedGroups);
            Assert.AreEqual(hash, session.ComputeDraftHash()); Assert.AreEqual(history, session.History.Count);
        }
        static void Accepts(EditSession session, EditBatch batch)
            => Assert.IsTrue(session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "valid", new ApplyReport()));
        [Test] public void NotFound_RemoveMissing()
        {
            using var f = Fixture(); using var s = Session(f); using var b = new EditBatch();
            Remove(b, Record(f.Hull)); Refuses(s, b, PlacementError.NotFound);
        }
        [Test] public void Occupied_ExistingCell()
        {
            using var f = Fixture(); Place(f, f.Hull); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Hull)); Refuses(s, b, PlacementError.Occupied);
        }
        [Test] public void InOtherClearance_ReservedCell()
        {
            using var f = Fixture(); Place(f, Id(f, "block_reservation")); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Hull, 1)); Refuses(s, b, PlacementError.InOtherClearance);
        }
        [Test] public void ClearanceBlocked_ExistingCell()
        {
            using var f = Fixture(); Place(f, f.Hull, 1); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(Id(f, "block_reservation"))); Refuses(s, b, PlacementError.ClearanceBlocked);
        }
        [Test] public void NoAttachment_IsolatedAddition()
        {
            using var f = Fixture(); Place(f, f.Hull); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Hull, 3)); Refuses(s, b, PlacementError.NoAttachment);
        }
        [Test] public void AttachFaceMismatch_NoAttachment()
        {
            using var f = Fixture(); Place(f, f.Hull); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(Id(f, "block_no_attach"), 1)); Refuses(s, b, PlacementError.NoAttachment);
        }
        [Test] public void CoreLimit_SecondCore()
        {
            using var f = Fixture(); ushort core = Id(f, "block_core"); Place(f, core); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(core, 1)); Refuses(s, b, PlacementError.CoreLimit);
        }
        [Test] public void OverBudget_TooManyCells()
        {
            using var f = Fixture(); f.Context.Config.MaxCells = 1; Place(f, f.Hull); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Hull, 1)); Refuses(s, b, PlacementError.OverBudget);
        }
        [Test] public void OutOfBounds_BuildLimitAndExtent()
        {
            using var f = Fixture(); Place(f, f.Hull); using var s = Session(f); using var b = new EditBatch(); Add(b, Record(f.Hull, 1));
            f.Context.Config.BuildLimitMax = Vector3Int.zero; Refuses(s, b, PlacementError.OutOfBounds);
            f.Context.Config.BuildLimitMax = new Vector3Int(511, 255, 511); f.Context.Config.MaxExtentCells = Vector3Int.one;
            Refuses(s, b, PlacementError.OutOfBounds);
        }
        [Test] public void OrientationNotAllowed_UprightOnly()
        {
            using var f = Fixture(); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(Id(f, "block_upright"), 0, 1)); Refuses(s, b, PlacementError.OrientationNotAllowed);
        }
        [Test] public void NotAvailable_MissingBlock()
        {
            using var f = Fixture(); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Catalog.GetBlockIndex(f.Catalog.MissingBlock))); Refuses(s, b, PlacementError.NotAvailable);
        }
        [Test] public void SymmetryOverlap_SameGroup()
        {
            using var f = Fixture(); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Hull)); Add(b, Record(f.Hull)); Refuses(s, b, PlacementError.SymmetryOverlap);
        }
        [Test] public void ReservedMissingCell_Occupied()
        {
            using var f = Fixture(); Place(f, f.Multi); f.Edits(new VoxelEdit { Kind = VoxelEditKind.ClearCell });
            using var s = Session(f); using var b = new EditBatch(); Add(b, Record(f.Hull)); Refuses(s, b, PlacementError.Occupied);
            b.Clear(); Remove(b, Record(f.Multi)); Add(b, Record(f.Hull)); Accepts(s, b);
            Assert.AreEqual(1, s.Draft.SolidCellCount); Assert.IsTrue(s.Undo());
            Assert.IsTrue(s.Draft.GetCell(int3.zero).IsAir); Assert.IsTrue(s.Placement.IsReservedMissing(int3.zero));
            // Imported damaged footprints may share an origin with a different live block (ADR-0082).
            using var other = Fixture(); Place(other, other.Multi); other.Edits(new VoxelEdit { Kind = VoxelEditKind.ClearCell });
            Place(other, other.Hull); using var shared = Session(other); b.Clear();
            Remove(b, Record(other.Multi)); Add(b, Record(other.Hull)); Refuses(shared, b, PlacementError.Occupied);
            b.Clear(); Remove(b, Record(other.Multi)); Accepts(shared, b);
            Assert.AreEqual(1, shared.Draft.SolidCellCount); Assert.AreEqual(other.Hull, new DraftReader(shared.Draft, other.Context).TryGetBlockAt(int3.zero, out var survivor) ? survivor.Block : 0);
            Assert.IsTrue(shared.Undo()); Assert.AreEqual(4, shared.Draft.SolidCellCount);
        }
        [Test] public void PerGroup_RollsBackFailedGroup()
        {
            using var f = Fixture(); Place(f, f.Hull); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Hull, 1)); b.Groups.Add(new BatchGroup { OpStart = 0, OpCount = 1 });
            Add(b, Record(f.Hull, 2)); Add(b, Record(f.Hull, 2)); b.Groups.Add(new BatchGroup { OpStart = 1, OpCount = 2 });
            Add(b, Record(f.Hull, 2)); b.Groups.Add(new BatchGroup { OpStart = 3, OpCount = 1 });
            var report = new ApplyReport(); Assert.IsTrue(s.TryApply(b, AtomicityMode.PerGroup, 0, ToolId.Place, "groups", report));
            CollectionAssert.AreEqual(new[] { PlacementError.None, PlacementError.SymmetryOverlap, PlacementError.None }, report.GroupErrors);
            Assert.AreEqual(2, report.AcceptedGroups); Assert.AreEqual(2, report.OpsApplied); Assert.AreEqual(3, s.Draft.SolidCellCount);
            Assert.IsTrue(s.Undo()); Assert.AreEqual(1, s.Draft.SolidCellCount);
        }
        [Test] public void AllOrNothing_RejectsAll()
        {
            using var f = Fixture(); Place(f, f.Hull); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Hull, 1)); Add(b, Record(f.Hull, 4));
            b.Groups.Add(new BatchGroup { OpStart = 0, OpCount = 1 }); b.Groups.Add(new BatchGroup { OpStart = 1, OpCount = 1 });
            Refuses(s, b, PlacementError.NoAttachment); Assert.AreEqual(1, s.Draft.SolidCellCount);
        }
        [Test] public void FirstBlock_NoAttachmentNeeded()
        {
            using var f = Fixture(); using var s = Session(f); using var b = new EditBatch();
            Add(b, Record(f.Multi, -4)); Accepts(s, b); Assert.AreEqual(4, s.Draft.SolidCellCount);
        }
        [Test] public void AabbShrinksAfterRemove()
        {
            using var f = Fixture(); Place(f, f.Hull, -20); Place(f, f.Hull, 10); using var s = Session(f); using var b = new EditBatch();
            Remove(b, Record(f.Hull, -20)); Accepts(s, b);
            Assert.IsTrue(s.Placement.TryGetBounds(out var min, out var max)); Assert.AreEqual(new int3(10, 0, 0), min); Assert.AreEqual(min, max);
            b.Clear(); Remove(b, Record(f.Hull, 10)); Accepts(s, b); Assert.IsFalse(s.Placement.TryGetBounds(out _, out _));
            b.Clear(); Add(b, Record(f.Hull, 30)); Accepts(s, b); Assert.IsTrue(s.Placement.TryGetBounds(out min, out max));
            Assert.AreEqual(new int3(30, 0, 0), min); Assert.AreEqual(min, max);
        }
    }
}
