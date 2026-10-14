using System;
using Game.App.Session;
using Game.Core;
using Game.Core.Events;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.Economy;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class CommitTests
    {
        [TearDown] public void Cleanup()=>TestContentFactory.DestroyAll();
        sealed class Fixture : IDisposable, IEditCommitTarget
        {
            internal readonly DraftAdapterTests.Fixture Content;
            internal readonly FleetModel Fleet=new();
            internal readonly InventoryModel Inventory;
            internal bool Reject;
            internal int Calls;
            internal ushort Hull=>Content.Hull;
            internal ContentId HullId=>Content.Context.Items.ItemOf(Hull);
            internal VoxelStructure Ship=>Fleet.Flagship.Structure;
            internal Fixture(int hulls=11,bool damage=false)
            {
                Content=new DraftAdapterTests.Fixture(defs=>defs.Add(TestContentFactory.Block("block_core",BlockKind.Functional,
                    modules:new BlockModule[]{new CoreModule{Params=new CoreParams{MaxCells=100,MaxExtentCells=100}}})));
                ushort core=Content.Catalog.GetBlockIndex(Content.Catalog.Get<BlockDefinition>(new ContentId("block_core")));
                Content.Edits(new VoxelEdit{Kind=VoxelEditKind.PlaceBlock,Block=core});
                for(int x=1;x<=hulls;x++)
                {
                    Content.Edits(new VoxelEdit{Kind=VoxelEditKind.PlaceBlock,Block=Hull,Cell=new int3(x,0,0)});
                    if(damage && x>=hulls-1) Content.Edits(new VoxelEdit{Kind=VoxelEditKind.SetCellHp,Cell=new int3(x,0,0),Hp=42});
                }
                Fleet.Add(new FleetShipRecord{PersistentId=42,Name="Editor fixture",Structure=Content.Draft.Clone()},true);
                Inventory=new InventoryModel(Content.Catalog);
            }
            internal EditSession Session(bool creative=false)=>new(new ShipEditorEntryArgs{Source=Ship,Inventory=Inventory,CommitTarget=this,PersistentId=42,Options=new EditSessionOptions{Creative=creative}},Content.Context);
            public bool TryCommitStructure(VoxelStructure committed,out string error)
            {
                Calls++; if(Reject){error="target refused";return false;}
                Fleet.ReplaceStructure(42,committed);error=null;return true;
            }
            internal void Resize(EditSession s,int count)
            {
                using var batch=new EditBatch();int old=s.Draft.SolidCellCount-1;
                for(int x=old;x>count;x--) batch.Ops.Add(new DiffOp{Kind=DiffOpKind.Remove,Record=new BlockRecord{Block=Hull,Origin=new int3(x,0,0),DamageOffset=-1}});
                for(int x=old+1;x<=count;x++) batch.Ops.Add(new DiffOp{Kind=DiffOpKind.Add,Record=new BlockRecord{Block=Hull,Origin=new int3(x,0,0),DamageOffset=-1}});
                if(batch.Ops.Length>0)Assert.IsTrue(s.TryApply(batch,AtomicityMode.AllOrNothing,0,ToolId.Place,"resize",new ApplyReport()));
            }
            internal void Give(int count)=>Assert.IsTrue(Inventory.TryAdd(Content.Catalog.GetBlock(Hull),count,AcquireSource.EditorRefund,out _));
            internal ulong Hash(VoxelStructure source)=>new DraftReader(source,Content.Context).ComputeDraftHash();
            public void Dispose(){Fleet.Dispose();Content.Dispose();}
        }
        [Test] public void RemoveThenDiscard_NoChange_R15()
        {
            using var f=new Fixture();using var s=f.Session();ulong original=f.Hash(f.Ship);f.Resize(s,0);Assert.AreEqual(11,s.GetBom().TotalRefunded);
            s.Discard();Assert.AreEqual(original,s.ComputeDraftHash());Assert.AreEqual(original,f.Hash(f.Ship));Assert.AreEqual(0,f.Inventory.CountOf(f.HullId));Assert.AreEqual(0,f.Calls);
        }
        [Test] public void RemoveThenCommit_RefundOnce_R15()
        {
            using var f=new Fixture();using var s=f.Session();f.Resize(s,0);var result=s.Commit();Assert.AreEqual(CommitStatus.Committed,result.Status,result.Message);
            Assert.AreEqual(11,result.ItemsRefunded);Assert.AreEqual(11,f.Inventory.CountOf(f.HullId));Assert.AreEqual(1,f.Ship.SolidCellCount);Assert.AreEqual(42ul,f.Fleet.Flagship.PersistentId);
            Assert.IsFalse(s.IsDirty);Assert.AreEqual(CommitStatus.NothingToCommit,s.Commit().Status);Assert.AreEqual(11,f.Inventory.CountOf(f.HullId));Assert.AreEqual(1,f.Calls);
        }
        [Test] public void MissingItems_NoChange()
        {
            using var f=new Fixture(0);using var s=f.Session();ulong original=f.Hash(f.Ship);f.Resize(s,2);ulong draft=s.ComputeDraftHash();var result=s.Commit();
            Assert.AreEqual(CommitStatus.MissingItems,result.Status);StringAssert.Contains("Missing 2",result.Message);Assert.AreEqual(original,f.Hash(f.Ship));Assert.AreEqual(draft,s.ComputeDraftHash());Assert.IsTrue(s.IsDirty);Assert.AreEqual(0,f.Calls);
        }
        [Test] public void CargoFull_NoChange()
        {
            using var f=new Fixture();f.Inventory.SetCapacity(1);var filler=f.Content.Catalog.Get<BlockDefinition>(new ContentId("block_core"));Assert.IsTrue(f.Inventory.TryAdd(filler,filler.MaxStack,AcquireSource.EditorRefund,out _));using var s=f.Session();ulong original=f.Hash(f.Ship);f.Resize(s,0);var result=s.Commit();
            Assert.AreEqual(CommitStatus.CargoFull,result.Status);Assert.AreEqual(original,f.Hash(f.Ship));Assert.AreEqual(0,f.Inventory.CountOf(f.HullId));Assert.IsTrue(s.IsDirty);Assert.AreEqual(0,f.Calls);
        }
        [Test] public void TargetRejects_InventoryRolledBack()
        {
            using var f=new Fixture(0);f.Inventory.SetCapacity(2);f.Give(1);Assert.IsTrue(f.Inventory.TryMove(0,1));f.Inventory.SetCapacity(1);
            var before=new ItemStack[InventoryModel.MaxSlots];for(int i=0;i<before.Length;i++)before[i]=f.Inventory.Get(i);
            using var s=f.Session();f.Resize(s,1);f.Reject=true;ulong draft=s.ComputeDraftHash(),ship=f.Hash(f.Ship);var result=s.Commit();
            Assert.AreEqual(CommitStatus.TargetRejected,result.Status);Assert.AreEqual("target refused",result.Message);Assert.AreEqual(1,f.Inventory.Capacity);
            for(int i=0;i<before.Length;i++){var after=f.Inventory.Get(i);Assert.AreEqual(before[i].Item,after.Item);Assert.AreEqual(before[i].Count,after.Count);Assert.AreEqual(before[i].OriginalId,after.OriginalId);}
            Assert.AreEqual(1,f.Inventory.CountOf(f.HullId));Assert.AreEqual(draft,s.ComputeDraftHash());Assert.AreEqual(ship,f.Hash(f.Ship));Assert.IsTrue(s.IsDirty);
        }
        [Test] public void RandomSessions_ItemsConserved()
        {
            using var f=new Fixture(12,true);f.Give(100);int expected=112;var rng=RngStreams.For(851,RngPurpose.ShipEditor,0);
            for(int i=0;i<100;i++)
            {
                using var s=f.Session();f.Resize(s,rng.NextInt(25));int lost=s.GetBom().TotalLostDamaged;
                if(rng.NextInt(2)==0){s.Discard();}else {var result=s.Commit();Assert.That(result.Status,Is.EqualTo(CommitStatus.Committed).Or.EqualTo(CommitStatus.NothingToCommit),result.Message);if(result.Status==CommitStatus.Committed)expected-=lost;}
                Assert.AreEqual(expected,f.Inventory.CountOf(f.HullId)+f.Ship.SolidCellCount-1,"Session "+i);
            }
        }
        [Test] public void Creative_NoInventoryTouch()
        {
            using var f=new Fixture(0);f.Inventory.SetCapacity(1);using var s=f.Session(true);f.Resize(s,5);int changes=0;f.Inventory.Changed+=()=>changes++;
            var result=s.Commit();Assert.AreEqual(CommitStatus.Committed,result.Status,result.Message);Assert.AreEqual(0,changes);Assert.AreEqual(0,result.ItemsUsed);Assert.AreEqual(0,result.ItemsRefunded);Assert.AreEqual(6,f.Ship.SolidCellCount);
        }
        [Test] public void CommitThenKeepEditing_NewBaseline()
        {
            using var f=new Fixture(3);using var s=f.Session();f.Resize(s,1);Assert.AreEqual(CommitStatus.Committed,s.Commit().Status);
            Assert.AreEqual(0,s.GetBom().Lines.Count);Assert.AreEqual(s.GetStats(false).Blocks,s.GetStats(true).Blocks);Assert.AreEqual(0,s.History.Count);
            f.Resize(s,2);Assert.AreEqual(1,s.GetBom().TotalRequired);Assert.AreEqual(CommitStatus.Committed,s.Commit().Status);Assert.AreEqual(1,f.Inventory.CountOf(f.HullId));
            ulong saved=s.ComputeDraftHash();f.Resize(s,0);s.Discard();Assert.AreEqual(saved,s.ComputeDraftHash());Assert.AreEqual(saved,f.Hash(f.Ship));Assert.AreEqual(2,f.Calls);
        }
    }
}
