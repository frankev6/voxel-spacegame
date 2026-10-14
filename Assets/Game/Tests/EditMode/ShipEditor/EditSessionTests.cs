using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class EditSessionTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static EditSession Session(DraftAdapterTests.Fixture f) => new(new ShipEditorEntryArgs {
            Source = f.Draft, Options = new EditSessionOptions { Creative = true } }, f.Context);
        static EditBatch Add(ushort block, int3 origin)
        {
            var batch = new EditBatch(); batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add,
                Record = new BlockRecord { Block = block, Origin = origin, DamageOffset = -1 } }); return batch;
        }
        [Test] public void ApplyUndoRedo_HashesMatch()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Hp = 37 });
            using var session = Session(f); using var batch = new EditBatch(); var report = new ApplyReport();
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = new BlockRecord { Block = f.Hull, DamageOffset = -1 } });
            ulong initial = session.ComputeDraftHash(); int notifications = 0; session.Changed += _ => notifications++;
            Assert.IsTrue(session.TryApply(batch, AtomicityMode.AllOrNothing, 1, ToolId.Remove, "remove", report));
            ulong removed = session.ComputeDraftHash(); Assert.AreNotEqual(initial, removed); Assert.AreEqual(1, report.OpsApplied);
            Assert.IsTrue(session.Undo()); Assert.AreEqual(initial, session.ComputeDraftHash()); Assert.AreEqual(37, session.Draft.GetCell(int3.zero).Hp);
            Assert.IsTrue(session.Redo()); Assert.AreEqual(removed, session.ComputeDraftHash()); Assert.AreEqual(3, notifications);
        }
        [Test] public void BoundaryEdit_DirtiesNeighbour()
        {
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f); using var dirty = new NativeList<int3>(Allocator.Temp);
            session.DrainDirtyBricks(dirty); using var batch = Add(f.Hull, new int3(15, 4, 4));
            session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "place", new ApplyReport()); session.DrainDirtyBricks(dirty);
            Assert.AreEqual(2, dirty.Length); CollectionAssert.AreEquivalent(new[] { int3.zero, new int3(1, 0, 0) }, dirty.AsArray().ToArray());
        }
        [Test] public void InteriorEdit_DirtiesOneBrick()
        {
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f); using var dirty = new NativeList<int3>(Allocator.Temp);
            session.DrainDirtyBricks(dirty); using var batch = Add(f.Hull, new int3(4, 4, 4));
            session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "place", new ApplyReport()); session.DrainDirtyBricks(dirty);
            Assert.AreEqual(1, dirty.Length); Assert.AreEqual(int3.zero, dirty[0]);
        }
        [Test] public void OriginalNeverMutated()
        {
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f);
            ulong original = new DraftReader(session.Original, f.Context).ComputeDraftHash();
            var random = Game.Core.RngStreams.For(0x12345678, Game.Core.RngPurpose.ShipEditor, 5);
            for (int i = 0; i < 100; i++)
            {
                using var batch = Add(f.Hull, new int3(i, 0, 0));
                var op = batch.Ops[0]; op.Record.Color = (byte)random.NextInt(16); batch.Ops[0] = op;
                Assert.IsTrue(session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "place", new ApplyReport()));
                Assert.AreEqual(original, new DraftReader(session.Original, f.Context).ComputeDraftHash());
            }
            Assert.AreEqual(original, f.Reader.ComputeDraftHash());
        }
        [Test] public void Discard_RestoresOriginal()
        {
            using var f = new DraftAdapterTests.Fixture(); using var session = Session(f); ulong original = session.ComputeDraftHash();
            using var batch = Add(f.Hull, new int3(20, 4, 4)); session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "place", new ApplyReport());
            using var dirty = new NativeList<int3>(Allocator.Temp); session.DrainDirtyBricks(dirty); session.Discard();
            Assert.AreEqual(original, session.ComputeDraftHash()); Assert.IsFalse(session.IsDirty); Assert.AreEqual(0, session.History.Count);
            session.DrainDirtyBricks(dirty); CollectionAssert.Contains(dirty.AsArray().ToArray(), new int3(1, 0, 0));
        }
        [Test] public void Dispose_NoLeaks()
        {
            var previous = NativeLeakDetection.Mode; NativeLeakDetection.Mode = NativeLeakDetectionMode.EnabledWithStackTrace;
            try
            {
                using var f = new DraftAdapterTests.Fixture(); var session = Session(f); using var batch = Add(f.Hull, int3.zero);
                session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "place", new ApplyReport()); var command = session.History[0];
                session.Dispose(); session.Dispose(); Assert.IsTrue(command.IsDisposed); Assert.AreEqual(0, f.Draft.SolidCellCount);
                Assert.Throws<System.ObjectDisposedException>(() => session.ComputeDraftHash());
            }
            finally { NativeLeakDetection.Mode = previous; }
        }
    }
}
