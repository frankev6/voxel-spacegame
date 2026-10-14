using System;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using Game.Data.Definitions;
using Game.Economy;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.UI.Data;
using Game.UI.ViewModels;
using Game.UI.Views;
using Unity.Mathematics;
using Unity.Profiling;

namespace Game.App.EditorHost
{
    /// <summary>Formats editor state only on refresh; commands retain the actual session's validation/undo/commit owners.</summary>
    internal sealed class ShipEditorPresenter : IShipEditorCommands, IBlueprintBrowserCommands, IDisposable
    {
        readonly ShipEditorEntryArgs args;
        readonly EditorHostServices services;
        readonly EditSession session;
        readonly EditorContext context;
        readonly ShipEditorController controller;
        readonly BlueprintBrowserActions browser;
        readonly List<EditIssue> issues=new();
        readonly List<ItemDelta> deltas=new();
        readonly List<ItemShortfall> shortfalls=new();
        readonly ProfilerMarker marker=new("ShipEditorPresenter.Refresh");
        ShipEditorViewModel Model=>services.Model;
        bool disposed,dirty=true,wasStroke,prompt;
        float remaining;
        int state=-1,revision=-1;
        internal int RefreshCount{get;private set;}
        internal ShipEditorPresenter(ShipEditorEntryArgs args,EditorHostServices services,EditSession session,EditorContext context,ShipEditorController controller)
        {
            this.args=args;this.services=services;this.session=session;this.context=context;this.controller=controller;
            if(services.Blueprints!=null)browser=new BlueprintBrowserActions(services.Blueprints,session,context,controller,services.BrowserModel,()=>services.Ui.Navigator.Pop(services.Browser),Status);
            Model.Title=args.Options.Title??"Ship editor";Model.IsCreative=args.Options.Creative;
            Model.Palette.Categories.Clear();Model.Palette.Categories.Add("All");foreach(var category in context.Categories)Model.Palette.Categories.Add(category.ToString());Model.Palette.NotifyCategories();
            Model.Palette.SelectedCategory=0;Model.Palette.SearchText="";
            for(int i=0;i<Model.Palette.Colors.Count;i++)Model.Palette.Colors[i].Swatch=services.PaintColors!=null && i<services.PaintColors.Length?services.PaintColors[i]:UnityEngine.Color.white;
            Model.Palette.NotifyColors();session.Changed+=Changed;if(args.Inventory!=null)args.Inventory.Changed+=InventoryChanged;
            Refresh();
        }
        void Changed(EditChange value)=>dirty=true;
        void InventoryChanged()=>dirty=true;
        internal void Tick(float dt,bool stroke)
        {
            if(disposed)return;remaining-=math.max(0,dt);
            dirty|=state!=controller.StateVersion || revision!=session.Revision;
            if(dirty && (remaining<=0 || wasStroke && !stroke))Refresh();
            wasStroke=stroke;
        }
        void Refresh()
        {
            using var scope=marker.Auto();RefreshCount++;dirty=false;remaining=math.max(.01f,context.Config.PresenterRefreshSeconds);
            state=controller.StateVersion;revision=session.Revision;
            Model.ActiveTool=(int)controller.ActiveTool;Model.IsDirty=session.IsDirty;
            Model.CanUndo=session.History.CanUndo;Model.CanRedo=session.History.CanRedo;
            Model.UndoLabel=Model.CanUndo?session.History[session.History.Cursor-1].Label:"Undo";Model.RedoLabel=Model.CanRedo?session.History[session.History.Cursor].Label:"Redo";
            var sym=controller.Symmetry;Model.MirrorX=sym.MirrorX;Model.MirrorY=sym.MirrorY;Model.MirrorZ=sym.MirrorZ;Model.RadialFold=sym.RadialFold;
            Model.SliceEnabled=controller.SliceEnabled;Model.SliceLevel=controller.SliceLevel;Model.XRay=controller.XRay;Model.BoxMode=(int)controller.BoxMode;
            Model.OrientationText="Rot "+controller.Orient;Model.HoverText=HoverText();
            Model.SelectedCount=controller.SelectedCount;Model.Palette.SelectedColor=controller.Color;
            for(int i=0;i<Model.Palette.Colors.Count;i++)Model.Palette.Colors[i].IsSelected=i==controller.Color;
            Palette();Hotbar();Stats();Bom();session.GetIssues(issues);Model.Issues.Rows.Clear();int errors=0,warnings=0;bool floating=false;
            for(int i=0;i<issues.Count;i++){var issue=issues[i];if(issue.IsError)errors++;else warnings++;floating|=issue.Kind==EditIssueKind.Disconnected;Model.Issues.Rows.Add(new EditorIssueRowViewModel{Index=i,Text=issue.Kind+" ("+issue.Count+")",IsError=issue.IsError});}
            Model.Issues.ErrorCount=errors;Model.Issues.WarningCount=warnings;Model.Issues.CanRemoveFloating=floating;Model.Issues.NotifyRows();
            string reason=!session.IsDirty?"No changes to commit":errors>0?Model.Issues.Rows[0].Text:args.CommitTarget==null?"Commit target unavailable":"";
            if(reason.Length==0 && !args.Options.Creative)
            {
                if(args.Inventory==null)reason="Inventory unavailable";
                else {deltas.Clear();foreach(var line in session.GetBom().Lines)if(line.HasItem && line.Refunded!=line.Required)deltas.Add(new ItemDelta(line.Item,line.Refunded-line.Required));
                    if(!args.Inventory.CanApply(deltas.ToArray(),shortfalls,out bool full))reason=full?"Cargo full":shortfalls.Count>0?"Missing "+shortfalls[0].Missing+" "+shortfalls[0].Item.Value:"Items unavailable";}
            }
            Model.CanCommit=reason.Length==0;Model.CommitBlockedReason=reason;Model.CommitLabel=args.Options.Creative?"Save blueprint":"Commit";
        }
        // QA-001: the footer names what is under the pointer instead of printing the picker's debug state.
        string HoverText()
        {
            if(controller.Hover.Kind==DraftPicker.HitKind.None)return "";
            if(controller.Hover.Kind==DraftPicker.HitKind.Block && new DraftReader(session.Draft,context).TryGetBlockAt(controller.Hover.Cell,out var record))
                return services.Content.Catalog.GetBlock(record.Block)?.DisplayName??"Block";
            return "Empty cell";
        }
        void Palette()
        {
            var model=Model.Palette;model.Items.Clear();
            foreach(var block in services.Content.Catalog.All<BlockDefinition>())
            {
                ushort index=services.Content.Catalog.GetBlockIndex(block);
                if(!context.Items.HasItem(index))continue;
                if(model.SelectedCategory>0 && (int)block.BlockCategory!=(int)context.Categories[model.SelectedCategory-1])continue;
                if(!string.IsNullOrEmpty(model.SearchText) && block.DisplayName.IndexOf(model.SearchText,StringComparison.OrdinalIgnoreCase)<0 && block.Id.Value.IndexOf(model.SearchText,StringComparison.OrdinalIgnoreCase)<0)continue;
                int available=args.Inventory?.CountOf(block.Id)??0;
                model.Items.Add(new EditorPaletteItemViewModel{BlockId=block.Id.Value,DisplayName=block.DisplayName,Icon=block.Icon,Available=available,ShowCount=!args.Options.Creative,IsSelected=index==controller.SelectedBlock,IsLocked=block.AllowedOrientations.Bits==0,LockReason=block.AllowedOrientations.Bits==0?"Unavailable orientation":""});
                if(index==controller.SelectedBlock)model.SelectedBlockId=block.Id.Value;
            }
            model.NotifyItems();
        }
        // QA-001 creative hotbar: seeded once from the palette; picking a block (palette or eyedropper) lands in the
        // active slot unless it is already on the bar, in which case that slot becomes active.
        bool hotbarSeeded;
        void Hotbar()
        {
            bool changed=false;
            if(!hotbarSeeded)
            {
                hotbarSeeded=true;int n=0;
                foreach(var block in services.Content.Catalog.All<BlockDefinition>())
                {
                    if(n>=Model.Hotbar.Length)break;ushort index=services.Content.Catalog.GetBlockIndex(block);
                    if(!context.Items.HasItem(index) || block.AllowedOrientations.Bits==0)continue;
                    Model.Hotbar[n++]=block.Id.Value;changed=true;
                }
            }
            var selected=controller.SelectedBlock!=0?services.Content.Catalog.GetBlock(controller.SelectedBlock):null;
            if(selected!=null)
            {
                int at=Array.IndexOf(Model.Hotbar,selected.Id.Value);
                if(at>=0)Model.HotbarIndex=at;
                else{Model.Hotbar[Model.HotbarIndex]=selected.Id.Value;changed=true;}
            }
            if(!changed)return;
            for(int i=0;i<Model.Hotbar.Length;i++)
            {
                var block=Model.Hotbar[i]!=null && services.Content.Catalog.TryGet<BlockDefinition>(new ContentId(Model.Hotbar[i]),out var definition)?definition:null;
                Model.HotbarIcons[i]=block?.Icon;Model.HotbarNames[i]=block?.DisplayName;
            }
            Model.HotbarVersion++;
        }
        public void SelectHotbar(int slot)
        {
            if(disposed || (uint)slot>=(uint)Model.Hotbar.Length)return;
            Model.HotbarIndex=slot;var id=Model.Hotbar[slot];
            if(id!=null){if(controller.ActiveTool!=ToolId.Place && controller.ActiveTool!=ToolId.Line && controller.ActiveTool!=ToolId.Box && controller.ActiveTool!=ToolId.Fill)controller.SelectTool(ToolId.Place);SelectBlock(id);}
            dirty=true;
        }
        public void TogglePalette(){if(!disposed)Model.PaletteOpen=!Model.PaletteOpen;}
        public void ToggleDetails(){if(!disposed)Model.DetailsOpen=!Model.DetailsOpen;}
        void Stats()
        {
            var now=session.GetStats(false);var before=session.GetStats(true);Model.Stats.Rows.Clear();
            Row("Blocks",now.Blocks,before.Blocks);Row("Cells",now.Cells,before.Cells);Row("Mass kg",now.MassKg,before.MassKg);Row("Thrust N",now.ForwardThrustN,before.ForwardThrustN);Row("Thrust / weight",now.ThrustToWeight,before.ThrustToWeight);
            Row("Power generation",now.PowerGen,before.PowerGen);Row("Power use",now.PowerUse,before.PowerUse);Row("Power storage",now.PowerStorage,before.PowerStorage);Row("DPS",now.Dps,before.Dps);Row("Shield",now.ShieldCapacity,before.ShieldCapacity);Row("Shield regen",now.ShieldRegen,before.ShieldRegen);Row("Cargo",now.CargoUnits,before.CargoUnits);Row("Cost",now.Cost,before.Cost);Model.Stats.NotifyRows();
        }
        void Row(string label,double value,double before)=>Model.Stats.Rows.Add(new StatRowViewModel{StatId=label,Label=label,Value=value.ToString("0.##",CultureInfo.InvariantCulture),Delta=(value-before).ToString("+0.##;-0.##;0",CultureInfo.InvariantCulture),DeltaSign=Math.Sign(value-before)});
        void Bom()
        {
            var bom=session.GetBom();Model.Bom.Rows.Clear();foreach(var line in bom.Lines)Model.Bom.Rows.Add(new EditorBomRowViewModel{ItemId=line.Item.Value,Name=services.Content.Catalog.GetBlock(line.Block).DisplayName,Required=line.Required,Refunded=line.Refunded,Available=line.Available,Shortfall=line.Shortfall,LostDamaged=line.LostDamaged});
            Model.Bom.TotalShortfall=bom.TotalShortfall;Model.Bom.HasShortfall=bom.TotalShortfall>0;Model.Bom.SummaryText="Uses "+bom.TotalRequired+", refunds "+bom.TotalRefunded+", damaged lost "+bom.TotalLostDamaged;Model.Bom.NotifyRows();
        }
        void Status(string text){if(!disposed)Model.StatusText=text??"";}
        public void SelectTool(int id){if(!disposed && Enum.IsDefined(typeof(ToolId),(byte)id))controller.SelectTool((ToolId)id);}
        public void SelectBlock(string id){if(!disposed && services.Content.Catalog.TryGet<BlockDefinition>(new ContentId(id),out var block))controller.SelectBlock(services.Content.Catalog.GetBlockIndex(block));}
        public void SelectColor(int i){if(!disposed && i>=0 && i<context.ColorCount)controller.SelectColor((byte)i);}
        public void SelectCategory(int i){if(disposed || i<0 || i>=Model.Palette.Categories.Count)return;Model.Palette.SelectedCategory=i;dirty=true;}
        public void SetSearch(string text){if(disposed)return;Model.Palette.SearchText=text??"";dirty=true;}
        public void Undo(){if(!disposed)session.Undo();}public void Redo(){if(!disposed)session.Redo();}
        public void ToggleMirror(int axis){if(!disposed && (uint)axis<3)controller.ToggleMirror(axis);}
        public void NudgeMirrorPlane(int axis,int amount){if(!disposed && (uint)axis<3)controller.NudgeMirrorPlane(axis,amount);}
        public void ToggleSlice(){if(!disposed)controller.ToggleSlice();}public void StepSlice(int delta){if(!disposed)controller.StepSlice(delta);}
        public void ToggleXRay(){if(!disposed)controller.ToggleXRay();}public void CycleBoxMode(){if(!disposed)controller.CycleBoxMode();}
        public void FocusIssue(int index){if(!disposed && (uint)index<issues.Count)services.Camera.FocusOn((float3)issues[index].FocusCell*session.Draft.CellSize,4*session.Draft.CellSize);}
        public void RemoveFloatingParts(){if(!disposed)session.RemoveFloatingParts(new ApplyReport());}
        public void RevertAll(){if(!disposed)session.Discard();}
        public void OpenSettings(){if(!disposed)services.ShowSettings?.Invoke();}
        public bool OnBackPressed(){if(!disposed)controller.Cancel();return true;}
        public void OpenBlueprintBrowser(){if(disposed)return;if(browser==null){Status("Blueprint store unavailable");return;}browser.Refresh();services.Ui.Navigator.Push(services.Browser);}
        public void Select(int index){if(!disposed)browser?.Select(index);}public void LoadStamp(){if(!disposed)browser?.LoadStamp();}public void LoadReplace(){if(!disposed)browser?.LoadReplace();}public void Delete(){if(!disposed)browser?.Delete();}
        public void SaveBlueprint()
        {
            if(disposed || prompt)return;if(services.Blueprints==null){Status("Blueprint store unavailable");return;}
            prompt=true;services.Ui.Prompt.Show(new TextPromptRequest{Title="Save blueprint",Label="Name",InitialText=args.Options.Title??"Blueprint",MaxLength=64},name=>
            {prompt=false;if(disposed)return;if(services.Blueprints.TrySave(name,session.Draft,out _,out var error))Status("Blueprint saved");else Status(error);},()=>prompt=false);
        }
        public void RequestCommit()=>Commit(false);
        void Commit(bool leave)
        {
            if(disposed || prompt)return;Refresh();if(!Model.CanCommit){Status(Model.CommitBlockedReason);return;}
            if(args.Options.Creative && services.SetBlueprintName!=null)
            {
                prompt=true;services.Ui.Prompt.Show(new TextPromptRequest{Title="Save blueprint",Label="Name",InitialText=args.Options.Title??"Blueprint",MaxLength=64},name=>{prompt=false;if(disposed)return;services.SetBlueprintName(name);FinishCommit(leave);},()=>prompt=false);
            }
            else FinishCommit(leave);
        }
        void FinishCommit(bool leave)
        {
            var result=session.Commit();Status(result.Message);Refresh();
            if(result.Status!=CommitStatus.Committed)return;
            services.Ui.Notifications.Post(new Notification{Kind=NotificationKind.Positive,Label=result.Message});if(leave)services.Leave?.Invoke();
        }
        public void RequestLeave()
        {
            if(disposed || prompt)return;if(!session.IsDirty){services.Leave?.Invoke();return;}Refresh();prompt=true;
            services.Ui.Confirm.Show(new ConfirmRequest{Title="Leave the editor?",Body=Model.Bom.SummaryText+"\n"+Model.CommitBlockedReason,CancelIndex=2,DefaultFocusIndex=2,Options=new[]{
                new ConfirmOption{Label=Model.CommitLabel,ElementName="confirm-commit",Variant=ButtonVariant.Primary,Enabled=Model.CanCommit,DisabledReason=Model.CommitBlockedReason},
                new ConfirmOption{Label="Discard",ElementName="confirm-discard",Variant=ButtonVariant.Danger,Enabled=true},
                new ConfirmOption{Label="Keep editing",ElementName="confirm-keep-editing",Variant=ButtonVariant.Secondary,Enabled=true}}},choice=>
                {prompt=false;if(disposed)return;if(choice==0)Commit(true);else if(choice==1){session.Discard();services.Leave?.Invoke();}});
        }
        public void Dispose(){if(disposed)return;disposed=true;session.Changed-=Changed;if(args.Inventory!=null)args.Inventory.Changed-=InventoryChanged;}
    }
}
