using System.Collections.Generic;
using Game.Core;
using Game.Data.Blocks;
using Game.Data.Definitions;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.ShipEditor.Validation;
using Game.Tests.EditMode.Data;
using Game.Voxel.Model;
using NUnit.Framework;
using Unity.Mathematics;

namespace Game.Tests.EditMode.ShipEditor
{
    public sealed class FullValidatorTests
    {
        [TearDown] public void Cleanup()=>TestContentFactory.DestroyAll();
        static DraftAdapterTests.Fixture Fixture()=>new(defs=> {
            defs.Add(TestContentFactory.Block("block_core",BlockKind.Functional,modules:new BlockModule[]{new CoreModule{Params=new CoreParams{MaxCells=100,MaxExtentCells=100}}}));
            defs.Add(TestContentFactory.Block("block_drain",BlockKind.Functional,modules:new BlockModule[]{new ThrusterModule{Params=new ThrusterParams{ThrustNewtons=10,PowerDraw=7}}}));
        });
        static ushort Id(DraftAdapterTests.Fixture f,string name)=>f.Catalog.GetBlockIndex(f.Catalog.Get<BlockDefinition>(new ContentId(name)));
        static void Place(DraftAdapterTests.Fixture f,ushort block,int x=0)=>f.Edits(new VoxelEdit{Kind=VoxelEditKind.PlaceBlock,Block=block,Cell=new int3(x,0,0)});
        static EditSession Session(DraftAdapterTests.Fixture f,bool creative=true)=>new(new ShipEditorEntryArgs{Source=f.Draft,Options=new EditSessionOptions{Creative=creative}},f.Context);
        static List<EditIssue> Issues(EditSession s,EditorContext c) {var result=new List<EditIssue>();FullValidator.Evaluate(s,c,result);bool warning=false;foreach(var i in result){if(!i.IsError)warning=true;else Assert.IsFalse(warning,"Errors precede warnings");}return result;}
        static EditIssue Has(EditSession s,EditorContext c,EditIssueKind kind) {var rows=Issues(s,c);Assert.IsTrue(rows.Exists(x=>x.Kind==kind),kind.ToString());return rows.Find(x=>x.Kind==kind);}
        [Test] public void NoCore_Error(){using var f=Fixture();using var s=Session(f);Assert.IsTrue(Has(s,f.Context,EditIssueKind.NoCore).IsError);}
        [Test] public void TwoCores_Error(){using var f=Fixture();Place(f,Id(f,"block_core"));Place(f,Id(f,"block_core"),1);using var s=Session(f);Assert.AreEqual(2,Has(s,f.Context,EditIssueKind.MultipleCores).Count);}
        [Test] public void Disconnected_ErrorWithFocus()
        {
            using var f=Fixture();Place(f,Id(f,"block_core"));Place(f,f.Hull,3);Place(f,f.Hull,4);Place(f,f.Hull,-3);using var s=Session(f);
            var issue=Has(s,f.Context,EditIssueKind.Disconnected);Assert.AreEqual(3,issue.Count);Assert.AreEqual(new int3(3,0,0),issue.FocusCell);
        }
        [Test] public void PowerDeficit_Warning(){using var f=Fixture();Place(f,Id(f,"block_drain"));using var s=Session(f);Assert.IsFalse(Has(s,f.Context,EditIssueKind.PowerDeficit).IsError);}
        [Test] public void NoThrust_Warning(){using var f=Fixture();Place(f,f.Hull);using var s=Session(f);Assert.IsFalse(Has(s,f.Context,EditIssueKind.NoThrust).IsError);}
        [Test] public void NoWeapons_Warning(){using var f=Fixture();Place(f,f.Hull);using var s=Session(f);Assert.IsFalse(Has(s,f.Context,EditIssueKind.NoWeapons).IsError);}
        [Test] public void OutOfBounds_Error(){using var f=Fixture();Place(f,f.Hull);Place(f,f.Hull,300);using var s=Session(f);Assert.IsTrue(Has(s,f.Context,EditIssueKind.OutOfBounds).IsError);}
        [Test] public void OverCellBudget_Error(){using var f=Fixture();Place(f,f.Hull);Place(f,f.Hull,1);f.Context.Config.MaxCells=1;using var s=Session(f);Assert.AreEqual(2,Has(s,f.Context,EditIssueKind.OverCellBudget).Count);}
        [Test] public void DamageInvariant_Error()
        {
            using var f=Fixture();using var s=Session(f);using var command=new DiffCommand(ToolId.Place,"corrupt damage",0);
            command.Damage.Add(2);for(int i=0;i<4;i++)command.Damage.Add(0);
            command.Ops.Add(new DiffOp{Kind=DiffOpKind.Add,Record=new BlockRecord{Block=f.Hull,DamageOffset=0}});
            s.DraftCounts.Apply(command,true,f.Context);Assert.AreEqual(1,Has(s,f.Context,EditIssueKind.DamageInvariant).Count);
        }
        [Test] public void UnbuildableBlock_Error()
        {
            using var f=Fixture();Place(f,Id(f,"block_missing"));using var s=Session(f,false);using var batch=new EditBatch();
            batch.Ops.Add(new DiffOp{Kind=DiffOpKind.Add,Record=new BlockRecord{Block=Id(f,"block_missing"),Origin=new int3(1,0,0),DamageOffset=-1}});
            Assert.IsTrue(s.TryApply(batch,AtomicityMode.AllOrNothing,0,ToolId.Place,"missing",new ApplyReport()));
            Assert.AreEqual(1,Has(s,f.Context,EditIssueKind.UnbuildableBlock).Count);
        }
        [Test] public void RemoveFloating_OneIslandUndoable()
        {
            using var f=Fixture();Place(f,Id(f,"block_core"));Place(f,f.Hull,1);Place(f,f.Hull,5);Place(f,f.Hull,6);using var s=Session(f);
            ulong before=s.ComputeDraftHash();var report=new ApplyReport();Assert.IsTrue(s.RemoveFloatingParts(report));Assert.AreEqual(2,report.OpsApplied);
            Assert.AreEqual(1,s.Connectivity.IslandCount);Assert.AreEqual(1,s.History.Count);Assert.AreEqual("Remove floating parts",s.History[0].Label);
            Assert.IsFalse(Issues(s,f.Context).Exists(x=>x.Kind==EditIssueKind.Disconnected));Assert.AreEqual(2,s.GetBom().TotalRefunded);
            Assert.IsTrue(s.Undo());Assert.AreEqual(before,s.ComputeDraftHash());Assert.AreEqual(2,s.Connectivity.IslandCount);
            Assert.IsTrue(s.Redo());Assert.AreEqual(1,s.Connectivity.IslandCount);Assert.IsFalse(s.RemoveFloatingParts(report));
        }
    }
}
