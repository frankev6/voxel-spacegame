using System.Collections.Generic;
using System.Diagnostics;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.ShipEditor.Tools;
using Game.Tests.Common.Perf;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.PerformanceTesting;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class BoxRemovalBatchTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static EditSession Session(DraftAdapterTests.Fixture f) => new(new ShipEditorEntryArgs {
            Source = f.Draft, Options = new EditSessionOptions { Creative = true } }, f.Context);
        static void Fill(DraftAdapterTests.Fixture f, int width, int depth)
        {
            var edits = new List<VoxelEdit>();
            for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
                edits.Add(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(x, 0, z) });
            edits.Add(new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(width / 2, 0, 0), Hp = 37 });
            f.Edits(edits.ToArray());
        }
        static List<BlockRecord> Selection(DraftAdapterTests.Fixture f, int x, int depth)
        {
            var records = new List<BlockRecord>();
            for (int z = 0; z < depth; z++)
            { Assert.IsTrue(f.Reader.TryGetBlockAt(new int3(x, 0, z), out var record)); records.Add(record); }
            return records;
        }
        static void Equal(EditSession reference, EditSession batched, int width, int depth)
        {
            Assert.AreEqual(reference.ComputeDraftHash(), batched.ComputeDraftHash());
            Assert.AreEqual(reference.Connectivity.IslandCount, batched.Connectivity.IslandCount);
            for (int x = 0; x < width; x++) for (int z = 0; z < depth; z++)
            {
                var cell = new int3(x, 0, z);
                int root = reference.Connectivity.RootAt(cell);
                Assert.AreEqual(root, batched.Connectivity.RootAt(cell), "root at " + cell);
                Assert.AreEqual(reference.Connectivity.SizeOf(root), batched.Connectivity.SizeOf(root));
            }
            Assert.AreEqual(reference.History.Count, batched.History.Count);
            Assert.AreEqual(reference.History.Cursor, batched.History.Cursor);
            var a = new List<EditIssue>(); var b = new List<EditIssue>();
            reference.RefreshIssuesNow(); batched.RefreshIssuesNow(); reference.GetIssues(a); batched.GetIssues(b);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i], b[i]);
        }
        [TestCase(false), TestCase(true)]
        public void SelectedRemoval_MatchesSequentialRootsDamageAndUndo(bool mirror)
        {
            using var f = new DraftAdapterTests.Fixture(); Fill(f, 9, 3);
            // Include a partially destroyed multi-cell block: missing footprint cells must stay missing after undo.
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi, Cell = new int3(20, 0, 0) },
                new VoxelEdit { Kind = VoxelEditKind.ClearCell, Cell = new int3(20, 0, 0) },
                new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(21, 0, 0), Hp = 41 });
            var selected = Selection(f, 4, 3);
            Assert.IsTrue(f.Reader.TryGetBlockAt(new int3(21, 0, 0), out var damaged)); selected.Add(damaged);
            using var reference = Session(f); using var batched = Session(f);
            using var oldTool = new ToolContext(reference, f.Context); using var newTool = new ToolContext(batched, f.Context);
            oldTool.Symmetry = newTool.Symmetry = new SymmetrySettings { MirrorX = mirror, PlaneX2 = 9 };
            oldTool.BeginStroke(); newTool.BeginStroke();
            foreach (var record in selected) oldTool.Remove(record, ToolId.Remove);
            newTool.RemoveSelected(selected, ToolId.Remove);
            Assert.AreEqual(oldTool.Error, newTool.Error); Equal(reference, batched, 24, 3);
            Assert.IsTrue(reference.Undo()); Assert.IsTrue(batched.Undo()); Equal(reference, batched, 24, 3);
            Assert.AreEqual(37, batched.Draft.GetCell(new int3(4, 0, 0)).Hp);
            Assert.AreEqual(41, batched.Draft.GetCell(new int3(21, 0, 0)).Hp);
            Assert.IsTrue(batched.Draft.GetCell(new int3(20, 0, 0)).IsAir);
            Assert.IsTrue(reference.Redo()); Assert.IsTrue(batched.Redo()); Equal(reference, batched, 24, 3);
        }
        [Test, Performance, Category("Performance")]
        public void SelectedRemoval_ProductionPathPaired()
        {
            PerfSettings.AssertReference();
            using var f = new DraftAdapterTests.Fixture(); Fill(f, 33, 32);
            var selected = Selection(f, 16, 32);
            var referenceMs = new double[300]; var batchedMs = new double[300];
            // Session construction and fixture restoration are excluded equally. Alternate order to limit warm-cache bias.
            for (int i = -60; i < 300; i++)
            {
                double a, b;
                if ((i & 1) == 0) { a = Measure(false); b = Measure(true); }
                else { b = Measure(true); a = Measure(false); }
                if (i >= 0) { referenceMs[i] = a; batchedMs[i] = b; }
            }
            new PerfRun(TestContext.CurrentContext.Test.FullName).AddSamples("SequentialRemovalMs", referenceMs)
                .AddSamples("BatchedRemovalMs", batchedMs).Write();
            double Measure(bool batch)
            {
                using var session = Session(f); using var tool = new ToolContext(session, f.Context); tool.BeginStroke();
                var watch = Stopwatch.StartNew();
                if (batch) tool.RemoveSelected(selected, ToolId.Remove);
                else foreach (var record in selected) tool.Remove(record, ToolId.Remove);
                session.CompleteRelabel(); watch.Stop();
                Assert.AreEqual(1024, session.Draft.SolidCellCount);
                Assert.AreEqual(2, session.Connectivity.IslandCount);
                return watch.Elapsed.TotalMilliseconds;
            }
        }
    }
}
