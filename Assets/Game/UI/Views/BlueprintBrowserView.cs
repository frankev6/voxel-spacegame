using Game.Core;
using Game.UI.Data;
using Game.UI.ViewModels;

namespace Game.UI.Views
{
    /// <summary>Implements SPEC-51 §4.4. Modal screen shell with literal metadata and typed constructor.</summary>
    public sealed class BlueprintBrowserView : UIScreen
    {
        readonly BlueprintBrowserViewModel viewModel;
        readonly IBlueprintBrowserCommands commands;

        public BlueprintBrowserView(BlueprintBrowserViewModel vm, IBlueprintBrowserCommands commands)
        {
            viewModel = vm;
            this.commands = commands;
        }

        public override string Key => "blueprint-browser";
        public override UILayer Layer => UILayer.Modals;
        public override InputContext Context => InputContext.Menu;
        public override bool PausesSimulation => false;
        public override bool BlocksWorldPointer => true;
        public override bool HidesHud => false;
        public override bool IsRoot => false;
        // TODO(EDT-020): authored browser elements and load/delete behavior.
        protected override void OnBuild() { }
    }
}
