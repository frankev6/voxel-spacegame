using System.Collections.Generic;
using Game.Core;
using Game.Economy;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class BomTests
    {
        [TearDown] public void Cleanup() => TestContentFactory.DestroyAll();
        static EditSession Session(DraftAdapterTests.Fixture f, InventoryModel inventory = null, bool creative = false)
            => new(new ShipEditorEntryArgs { Source = f.Draft, Inventory = inventory,
                Options = new EditSessionOptions { Creative = creative } }, f.Context);
        static BlockRecord Record(ushort block, int x, byte orient = 0)
            => new() { Block = block, Origin = new int3(x, 0, 0), Orient = orient, DamageOffset = -1 };
        static void Apply(EditSession session, EditBatch batch)
            => Assert.IsTrue(session.TryApply(batch, AtomicityMode.AllOrNothing, 0, ToolId.Place, "BOM check", new ApplyReport()));
        static void Populate(DraftAdapterTests.Fixture f, int count, bool damaged)
        {
            var edits = new List<VoxelEdit>();
            for (int i = 0; i < count; i++)
            {
                edits.Add(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Hull, Cell = new int3(i, 0, 0) });
                if (damaged) edits.Add(new VoxelEdit { Kind = VoxelEditKind.SetCellHp, Cell = new int3(i, 0, 0), Hp = 42 });
            }
            f.Edits(edits.ToArray());
        }
        [Test] public void RemoveIntact_Refunds()
        {
            using var f = new DraftAdapterTests.Fixture(); Populate(f, 11, false);
            var inventory = new InventoryModel(f.Catalog); using var session = Session(f, inventory); using var batch = new EditBatch();
            for (int i = 0; i < 11; i++) batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = Record(f.Hull, i) });
            Apply(session, batch); var bom = session.GetBom(); Assert.AreEqual(1, bom.Lines.Count);
            Assert.AreEqual(11, bom.TotalRefunded); Assert.AreEqual(0, bom.TotalRequired); Assert.AreEqual(0, bom.TotalLostDamaged);
            Assert.AreEqual(0, inventory.CountOf(f.Context.Items.ItemOf(f.Hull)), "Preview must not change cargo.");
            Assert.IsTrue(session.Undo()); Assert.AreEqual(0, session.GetBom().Lines.Count);
            Assert.IsTrue(session.Redo()); Assert.AreEqual(11, session.GetBom().TotalRefunded);
            session.Discard(); Assert.AreEqual(0, session.GetBom().Lines.Count);
        }
        [Test] public void ReplaceDamaged_RequiresAndLoses()
        {
            using var f = new DraftAdapterTests.Fixture(); Populate(f, 5, true);
            var inventory = new InventoryModel(f.Catalog); using var session = Session(f, inventory); using var batch = new EditBatch();
            for (int i = 0; i < 5; i++)
            {
                batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = Record(f.Hull, i) });
                batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Hull, i) });
            }
            Apply(session, batch); var bom = session.GetBom(); Assert.AreEqual(5, bom.TotalRequired);
            Assert.AreEqual(5, bom.TotalLostDamaged); Assert.AreEqual(0, bom.TotalRefunded); Assert.AreEqual(5, bom.TotalShortfall);
            Assert.IsTrue(inventory.TryAdd(f.Context.Items.ItemOf(f.Hull), 3, out _));
            Assert.AreSame(bom, session.GetBom()); Assert.AreEqual(3, bom.Lines[0].Available); Assert.AreEqual(2, bom.TotalShortfall);
            Assert.IsTrue(session.Undo()); Assert.AreEqual(0, session.GetBom().Lines.Count);
            Assert.IsTrue(session.Redo()); Assert.AreEqual(5, session.GetBom().TotalLostDamaged);
        }
        [Test] public void MoveDamaged_NoLine()
        {
            using var f = new DraftAdapterTests.Fixture();
            f.Edits(new VoxelEdit { Kind = VoxelEditKind.PlaceBlock, Block = f.Multi },
                new VoxelEdit { Kind = VoxelEditKind.ClearCell },
                new VoxelEdit { Kind = VoxelEditKind.SetFunctionHp, Block = f.Multi, FunctionHp = 0 });
            using var session = Session(f); using var batch = new EditBatch(); var record = Record(f.Multi, 0);
            record.DamageOffset = new DraftReader(session.Draft, f.Context).CaptureDamage(record, batch.Damage);
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Remove, Record = record });
            record.Origin = new int3(8, 0, 0); batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = record });
            Apply(session, batch); Assert.AreEqual(0, session.GetBom().Lines.Count);
            Assert.AreEqual(1, session.DraftCounts.Damaged(f.Multi)); Assert.AreEqual(0, session.DraftCounts.Intact(f.Multi));
            Assert.IsTrue(session.Undo()); Assert.AreEqual(0, session.GetBom().Lines.Count);
            Assert.IsTrue(session.Redo()); Assert.AreEqual(0, session.GetBom().Lines.Count);
        }
        [Test] public void CreativeAvailable_Zero()
        {
            using var f = new DraftAdapterTests.Fixture(); var inventory = new InventoryModel(f.Catalog);
            Assert.IsTrue(inventory.TryAdd(f.Context.Items.ItemOf(f.Hull), 8, out _));
            using var session = Session(f, inventory, true); using var batch = new EditBatch();
            batch.Ops.Add(new DiffOp { Kind = DiffOpKind.Add, Record = Record(f.Hull, 0) }); Apply(session, batch);
            var bom = session.GetBom(); Assert.AreEqual(1, bom.TotalRequired); Assert.AreEqual(0, bom.Lines[0].Available);
            Assert.IsFalse(bom.HasUnbuildable); Assert.AreEqual(8, inventory.CountOf(f.Context.Items.ItemOf(f.Hull)));
        }
        [Test] public void RandomEdits_CountsMatchEnumeration()
        {
            using var f = new DraftAdapterTests.Fixture(); Populate(f, 4, true); using var session = Session(f);
            var rng = RngStreams.For(0xED7006, RngPurpose.ShipEditor, 1);
            using var blocks = new NativeList<BlockRecord>(Allocator.TempJob); using var bytes = new NativeList<byte>(Allocator.Temp);
            using var batch = new EditBatch(); var reader = new DraftReader(session.Draft, f.Context);
            var intact = new int[f.Context.Items.Count]; var damaged = new int[intact.Length];
            var pairs = new Dictionary<(ushort Block, byte Orient), int>();
            for (int step = 0; step < 1000; step++)
            {
                int action = rng.NextInt(5);
                if (action == 0 && session.History.CanUndo) session.Undo();
                else if (action == 1 && session.History.CanRedo) session.Redo();
                else
                {
                    batch.Clear(); int x = rng.NextInt(16);
                    bool exists = reader.TryGetBlockAt(new int3(x, 0, 0), out var record);
                    if (!exists)
                    {
                        // Keep random additions attached; removals and undo may still split the tiny draft.
                        reader.Enumerate(blocks);
                        if (blocks.Length != 0)
                        {
                            x = blocks[0].Origin.x;
                            while (reader.TryGetBlockAt(new int3(x, 0, 0), out _)) x++;
                        }
                        record = Record(rng.NextBool() ? f.Hull : f.Single, x, (byte)rng.NextInt(24));
                    }
                    batch.Ops.Add(new DiffOp { Kind = exists ? DiffOpKind.Remove : DiffOpKind.Add, Record = record }); Apply(session, batch);
                }
                System.Array.Clear(intact, 0, intact.Length); System.Array.Clear(damaged, 0, damaged.Length); pairs.Clear();
                reader.Enumerate(blocks);
                foreach (var record in blocks)
                {
                    bytes.Clear(); bool hurt = reader.CaptureDamage(record, bytes) >= 0;
                    if (hurt) damaged[record.Block]++; else intact[record.Block]++;
                    var key = (record.Block, record.Orient); pairs.TryGetValue(key, out int n); pairs[key] = n + 1;
                }
                for (int block = 0; block < intact.Length; block++)
                { Assert.AreEqual(intact[block], session.DraftCounts.Intact(block)); Assert.AreEqual(damaged[block], session.DraftCounts.Damaged(block)); }
                Assert.AreEqual(pairs.Count, session.DraftCounts.Orientations.Count);
                foreach (var pair in pairs) Assert.AreEqual(pair.Value, session.DraftCounts.Orientations[pair.Key.Block * 24 + pair.Key.Orient]);
            }
        }
    }
}
