using System.Collections.Generic;
using Game.Data.Blocks;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Unity.Collections;
using Unity.Mathematics;

namespace Game.ShipEditor.Validation
{
    /// <summary>SPEC-51 §5.6. Full validation reads the draft and never changes it.</summary>
    internal static class FullValidator
    {
        internal static void Evaluate(EditSession session, EditorContext context, List<EditIssue> result)
        {
            session.CompleteRelabel(); result.Clear(); var indexes=session.Placement;
            if(indexes.Cores==0) Add(result,EditIssueKind.NoCore,1);
            else if(indexes.Cores>1) Add(result,EditIssueKind.MultipleCores,indexes.Cores);
            if(session.Connectivity.IslandCount>1)
            {
                using var records=new NativeList<BlockRecord>(Allocator.TempJob); new DraftReader(session.Draft,context).Enumerate(records);
                int keep=KeptRoot(session,context,records), count=0, largest=-1; int3 focus=default;
                foreach(var record in records)
                {
                    bool disconnected=false;
                    foreach(var offset in context.Footprints.Get(record.Block,record.Orient).Cells)
                    {
                        int3 cell=record.Origin+offset; if(!OwnsCell(session,record,cell)) continue;
                        int root=session.Connectivity.RootAt(cell); if(root==keep) continue; disconnected=true;
                        int size=session.Connectivity.SizeOf(root);
                        if(size>largest || size==largest && Before(cell,focus)) {largest=size;focus=cell;}
                    }
                    if(disconnected) count++;
                }
                Add(result,EditIssueKind.Disconnected,count,focus);
            }
            if(indexes.TryGetBounds(out int3 min,out int3 max) && math.any((double3)max-(double3)min+1 > (double3)(int3)context.Config.MaxExtentCells))
                Add(result,EditIssueKind.OutOfBounds,1,min);
            if(indexes.SolidCells>context.Config.MaxCells) Add(result,EditIssueKind.OverCellBudget,indexes.SolidCells);
            int damage=0;
            for(int i=0;i<session.DraftCounts.TypeCount;i++) damage+=math.max(0,session.DraftCounts.Damaged(i)-session.OriginalCounts.Damaged(i));
            if(damage>0) Add(result,EditIssueKind.DamageInvariant,damage);
            if(!session.Options.Creative)
            {
                int missing=0; foreach(var line in session.GetBom().Lines) if(line.Required>0 && !line.HasItem) missing+=line.Required;
                if(missing>0) Add(result,EditIssueKind.UnbuildableBlock,missing);
            }
            var stats=session.GetStats(false);
            if(stats.PowerUse>stats.PowerGen) Add(result,EditIssueKind.PowerDeficit,1);
            if(stats.ForwardThrustN<=0) Add(result,EditIssueKind.NoThrust,1);
            bool weapons=false;
            for(int i=1;i<session.DraftCounts.TypeCount;i++)
                if(session.DraftCounts.Intact(i)+session.DraftCounts.Damaged(i)>0 && context.Definitions.Value.Blocks[i].HasModule(BlockModuleId.WeaponMount)) {weapons=true;break;}
            if(!weapons) Add(result,EditIssueKind.NoWeapons,1);
        }
        internal static int KeptRoot(EditSession session,EditorContext context,NativeList<BlockRecord> records)
        {
            if(session.Placement.Core is BlockRecord core)
                foreach(var offset in context.Footprints.Get(core.Block,core.Orient).Cells)
                {var cell=core.Origin+offset;if(OwnsCell(session,core,cell))return session.Connectivity.RootAt(cell);}
            int best=-1,largest=0;
            foreach(var record in records) foreach(var offset in context.Footprints.Get(record.Block,record.Orient).Cells)
            {
                var cell=record.Origin+offset; if(!OwnsCell(session,record,cell))continue;
                int root=session.Connectivity.RootAt(cell),size=session.Connectivity.SizeOf(root);
                if(size>largest || size==largest && root<best) {best=root;largest=size;}
            }
            return best;
        }
        internal static bool OwnsCell(EditSession session,BlockRecord record,int3 cell) =>
            session.Draft.TryGetBlockAt(cell,out var block,out _) && block.Origin.Equals(record.Origin) && block.Block==record.Block && block.Orient==record.Orient && block.Color==record.Color;
        static bool Before(int3 a,int3 b)=>a.x<b.x || a.x==b.x && (a.y<b.y || a.y==b.y && a.z<b.z);
        static void Add(List<EditIssue> result,EditIssueKind kind,int count,int3 focus=default)=>result.Add(new EditIssue {Kind=kind,Count=count,FocusCell=focus});
    }
}
