using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;
using System;
using UnityEngine.UIElements;

namespace Game.UI.Views
{
    /// <summary>SPEC-61 HUD-018: four authored editor regions, with an unpickable world centre.</summary>
    public sealed class ShipEditorView : UIScreen
    {
        readonly ShipEditorViewModel viewModel;
        readonly IShipEditorCommands commands;

        public ShipEditorView(ShipEditorViewModel vm, IShipEditorCommands commands)
        {
            viewModel = vm ?? throw new ArgumentNullException(nameof(vm));
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        public override string Key => "ship-editor";
        public override UILayer Layer => UILayer.Hud;
        public override InputContext Context => InputContext.Editor;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => false;
        public override bool HidesHud => false;
        public override bool IsRoot => true;
        protected override void OnBuild()
        {
            Root.dataSource = viewModel; Root.pickingMode = PickingMode.Ignore;
            ShipEditorPanels.Mount(Q<VisualElement>("ship-editor-region-top"), Q<VisualElement>("ship-editor-region-left"),
                Q<VisualElement>("ship-editor-region-right"), Q<VisualElement>("ship-editor-region-bottom"), viewModel, commands,
                Navigator.RootOwner ?? throw new InvalidOperationException("The ship editor requires its navigator's UI root."));
        }
        public override bool OnBack() => commands.OnBackPressed();
    }
}
