using System;
using Game.Data.Services;
using Game.Input;
using Game.Presentation.Services;
using Game.ShipEditor.Data;
using Game.ShipEditor.Services;
using Game.UI.Services;
using Game.UI.ViewModels;
using Game.UI.Views;
using UnityEngine;

namespace Game.App.EditorHost
{
    /// <summary>Persistent UI bindings; borrows application owners and forwards only to the current scene host.</summary>
    public sealed class EditorHostServices : IShipEditorCommands, IBlueprintBrowserCommands
    {
        public readonly UIRoot Ui;
        public readonly InputService Input;
        public readonly CameraRigService Camera;
        public readonly ContentService Content;
        public readonly ShipEditorConfig Config;
        public readonly IBlueprintStore Blueprints;
        public readonly ShipEditorViewModel Model = new();
        public readonly BlueprintBrowserViewModel BrowserModel = new();
        public readonly ShipEditorView View;
        public readonly BlueprintBrowserView Browser;
        public Action Leave, ShowSettings;
        public Action<string> SetBlueprintName;
        public Material PropMaterial;
        public Color[] PaintColors;
        internal ShipEditorPresenter Presenter;
        public EditorHostServices(UIRoot ui, InputService input, CameraRigService camera, ContentService content, ShipEditorConfig config, IBlueprintStore blueprints)
        {
            Ui=ui??throw new ArgumentNullException(nameof(ui));Input=input??throw new ArgumentNullException(nameof(input));
            Camera=camera??throw new ArgumentNullException(nameof(camera));Content=content??throw new ArgumentNullException(nameof(content));
            Config=config?config:throw new ArgumentNullException(nameof(config));Blueprints=blueprints;
            foreach(var render in content.Catalog.All<Game.Data.Definitions.VoxelRenderConfigDefinition>())
            {
                PropMaterial=render.PropMaterial;
                if(render.Palette!=null){PaintColors=new Color[16];for(int i=0;i<16;i++)PaintColors[i]=render.Palette.Colors[i];}break;
            }
            Model.Palette=new EditorPaletteViewModel();Model.Stats=new EditorStatsViewModel();Model.Bom=new EditorBomViewModel();Model.Issues=new EditorIssuesViewModel();
            View=new ShipEditorView(Model,this);Browser=new BlueprintBrowserView(BrowserModel,this);
            Ui.Register(View);Ui.Register(Browser);
        }
        public void SelectTool(int id)=>Presenter?.SelectTool(id);
        public void SelectBlock(string id)=>Presenter?.SelectBlock(id);
        public void SelectColor(int index)=>Presenter?.SelectColor(index);
        public void SelectCategory(int index)=>Presenter?.SelectCategory(index);
        public void SetSearch(string text)=>Presenter?.SetSearch(text);
        public void Undo()=>Presenter?.Undo();public void Redo()=>Presenter?.Redo();
        public void ToggleMirror(int axis)=>Presenter?.ToggleMirror(axis);
        public void NudgeMirrorPlane(int axis,int halfCells)=>Presenter?.NudgeMirrorPlane(axis,halfCells);
        public void ToggleSlice()=>Presenter?.ToggleSlice();public void StepSlice(int delta)=>Presenter?.StepSlice(delta);
        public void ToggleXRay()=>Presenter?.ToggleXRay();public void CycleBoxMode()=>Presenter?.CycleBoxMode();
        public void FocusIssue(int index)=>Presenter?.FocusIssue(index);public void RemoveFloatingParts()=>Presenter?.RemoveFloatingParts();
        public void RevertAll()=>Presenter?.RevertAll();public void SaveBlueprint()=>Presenter?.SaveBlueprint();
        public void OpenBlueprintBrowser()=>Presenter?.OpenBlueprintBrowser();public void RequestCommit()=>Presenter?.RequestCommit();
        public void RequestLeave()=>Presenter?.RequestLeave();public void OpenSettings()=>Presenter?.OpenSettings();
        public bool OnBackPressed(){Presenter?.OnBackPressed();return true;}
        public void Select(int index)=>Presenter?.Select(index);public void LoadStamp()=>Presenter?.LoadStamp();
        public void LoadReplace()=>Presenter?.LoadReplace();public void Delete()=>Presenter?.Delete();
    }
}
